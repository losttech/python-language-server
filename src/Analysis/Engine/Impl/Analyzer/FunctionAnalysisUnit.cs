// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    class FunctionAnalysisUnit : AnalysisUnit {
        public readonly FunctionInfo Function;

        internal readonly AnalysisUnit _declUnit;
        private readonly bool _concreteParameters;
        private readonly Dictionary<Node, Expression> _decoratorCalls;

        internal FunctionAnalysisUnit(
            FunctionInfo function,
            AnalysisUnit declUnit,
            InterpreterScope declScope,
            IPythonProjectEntry declEntry,
            bool concreteParameters
        )
            : base(function.FunctionDefinition, null) {
            _declUnit = declUnit;
            Function = function;
            _concreteParameters = concreteParameters;
            _decoratorCalls = new Dictionary<Node, Expression>();

            var scope = new FunctionScope(Function, Function.FunctionDefinition, declScope, declEntry);
            _scope = scope;

            if (GetType() == typeof(FunctionAnalysisUnit)) {
                AnalysisLog.NewUnit(this);
            }
        }

        internal virtual void EnsureParameters() {
            ((FunctionScope)Scope).EnsureParameters(this, usePlaceholders: !_concreteParameters);
        }

        internal virtual void EnsureParameterZero() {
            ((FunctionScope)Scope).EnsureParameterZero(this);
        }

        internal virtual bool UpdateParameters(ArgumentSet callArgs, bool enqueue = true) {
            return ((FunctionScope)Scope).UpdateParameters(this, callArgs, enqueue, null, usePlaceholders: !_concreteParameters);
        }

        internal void AddNamedParameterReferences(AnalysisUnit caller, NameExpression[] names) {
            ((FunctionScope)Scope).AddParameterReferences(caller, names);
        }

        internal override ModuleInfo GetDeclaringModule() {
            return base.GetDeclaringModule() ?? _declUnit.DeclaringModule;
        }

        IVariableDefinition GetExternalAnnotationVariableDefinition() {
            var parentAnnotation = _declUnit.GetExternalAnnotationAnalysisUnit();
            if (parentAnnotation == null)
                return null;
            return parentAnnotation.Scope.TryGetVariable(Ast.Name, out var annotationVariable)
                ? annotationVariable
                : null;
        }

        protected internal override AnalysisUnit GetExternalAnnotationAnalysisUnit() {
            var annotationVariable = this.GetExternalAnnotationVariableDefinition();
            if (annotationVariable == null)
                return null;
            if (annotationVariable.Types is FunctionInfo functionAnnotation)
                return functionAnnotation.AnalysisUnit;
            if (annotationVariable.Types.OnlyOneOrDefault() is FunctionInfo nestedAnnotation)
                return nestedAnnotation.AnalysisUnit;
            return null;
        }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            // Resolve default parameters and decorators in the outer scope but
            // continue to associate changes with this unit.
            ddg.Scope = _declUnit.InterpreterScope;
            AnalyzeDefaultParameters(ddg);

            var funcType = ProcessFunctionDecorators(ddg);
            EnsureParameterZero();

            _declUnit.InterpreterScope.AddLocatedVariable(Ast.Name, Ast.NameExpression, this);

            // Set the scope to within the function
            ddg.Scope = InterpreterScope;

            if (this.GetExternalAnnotationVariableDefinition() is VariableDef annotationsVariable) {
                annotationsVariable.AddDependency(this);
                if (annotationsVariable.Types is FunctionInfo functionInfo) {
                    functionInfo.FunctionAnalysisUnit.ReturnValue.AddDependency(this);
                    foreach (VariableDef parameterVariable in functionInfo.GetParameterVariables())
                        parameterVariable?.AddDependency(this);
                }
            }

            Ast.Body.Walk(ddg);

            _declUnit.InterpreterScope.AssignVariable(Ast.Name, Ast.NameExpression, this, funcType);
        }


        public new FunctionDefinition Ast => (FunctionDefinition)base.Ast;

        public VariableDef ReturnValue => FunctionScope.ReturnValue;

        private bool ProcessAbstractDecorators(IAnalysisSet decorator) {
            var res = false;

            // Only handle these if they are specialized
            foreach (var d in decorator.OfType<SpecializedCallable>()) {
                if (d.DeclaringModule != null
                    && d.DeclaringModule.ModuleName != "abc") {
                    continue;
                }

                switch (d.Name) {
                    case "abstractmethod":
                        res = true;
                        Function.IsAbstract = true;
                        break;
                    case "abstractstaticmethod":
                        Function.IsStatic = true;
                        Function.IsAbstract = true;
                        res = true;
                        break;
                    case "abstractclassmethod":
                        Function.IsClassMethod = true;
                        Function.IsAbstract = true;
                        res = true;
                        break;
                    case "abstractproperty":
                        this.ProcessGetterDecorator();
                        Function.IsAbstract = true;
                        res = true;
                        break;
                }
            }

            return res;
        }

        private bool ProcessSetterDecorator(Expression decorator, ExpressionEvaluator eval) {
            if (!(decorator is MemberExpression setter
                  && setter.Name == "setter"))
                return false;

            if (Function.FunctionDefinition.Parameters.Length != 2)
                // TODO: emit warning
                return false;

            if (!(this.Scope.OuterScope is ClassScope))
                // TODO: emit warning
                return false;

            var getter = eval.Evaluate(setter.Target);
            if (getter.Count == 0)
                return false;

            bool getterFound = false;
            foreach (var potentialGetter in getter.OfType<FunctionInfo>()) {
                if (ReferenceEquals(potentialGetter, Function)) continue;

                if (!potentialGetter.IsProperty)
                    continue;

                System.Diagnostics.Debug.Assert(!getterFound);

                potentialGetter.Property ??= new PropertyInfo {
                    Getter = potentialGetter,
                };
                potentialGetter.Property.Setter = Function;
                Function.Property = potentialGetter.Property;

                var valueParameter = FunctionScope.GetParameter(Function.FunctionDefinition.Parameters[1].Name);

                potentialGetter.FunctionAnalysisUnit.FunctionScope
                    .AddReturnTypes(decorator, this, valueParameter.Types);
                potentialGetter.PropagateReturnType();

                valueParameter.AddTypes(this, potentialGetter.GetReturnValue(), enqueue: false);
                Function.PropagateParameterTypes();

                getterFound = true;
            }
            return true;
        }

        private void ProcessGetterDecorator() {
            if (Function.FunctionDefinition.Parameters.Length != 1)
                // TODO: emit warning
                return;

            if (!(this.Scope.OuterScope is ClassScope))
                // TODO: emit warning
                return;

            // this assumes getters are always declared first
            System.Diagnostics.Debug.Assert(Function.Property == null || Function.IsGetter());
            Function.Property ??= new PropertyInfo {Getter = Function};
        }

        internal IAnalysisSet ProcessFunctionDecorators(DDG ddg) {
            var types = Function.SelfSet;
            if (Ast.Decorators != null) {
                Expression expr = Ast.NameExpression;

                foreach (var d in Ast.Decorators.Decorators.ExcludeDefault()) {
                    var decorator = ddg._eval.Evaluate(d);

                    if (decorator.Contains(State.ClassInfos[BuiltinTypeId.Property])) {
                        ProcessGetterDecorator();
                    } else if (decorator.Contains(State.ClassInfos[BuiltinTypeId.StaticMethod])) {
                        // TODO: Warn if IsClassMethod is set
                        Function.IsStatic = true;
                    } else if (decorator.Contains(State.ClassInfos[BuiltinTypeId.ClassMethod])) {
                        // TODO: Warn if IsStatic is set
                        Function.IsClassMethod = true;
                    } else if (ProcessAbstractDecorators(decorator)) {
                        // No-op
                    } else if (ProcessSetterDecorator(d, ddg._eval)) {
                        // No-op
                    } else {
                        Expression nextExpr;
                        if (!_decoratorCalls.TryGetValue(d, out nextExpr)) {
                            nextExpr = _decoratorCalls[d] = new CallExpression(d, new[] { new Arg(expr) });
                            nextExpr.SetLoc(d.IndexSpan);
                        }
                        expr = nextExpr;
                        var decorated = AnalysisSet.Empty;
                        var anyResults = false;
                        foreach (var ns in decorator) {
                            var fd = ns as FunctionInfo;
                            if (fd != null && InterpreterScope.EnumerateTowardsGlobal.Any(s => s.AnalysisValue == fd)) {
                                continue;
                            }
                            decorated = decorated.Union(ns.Call(expr, this, new[] { types }, ExpressionEvaluator.EmptyNames));
                            anyResults = true;
                        }

                        // If processing decorators, update the current
                        // function type. Otherwise, we are acting as if
                        // each decorator returns the function unmodified.
                        if (ddg.ProjectState.Limits.ProcessCustomDecorators && anyResults) {
                            types = decorated;
                        }
                    }
                }
            }

            return types;
        }

        internal void AnalyzeDefaultParameters(DDG ddg) {
            IVariableDefinition param;
            var annotationAnalysis = GetExternalAnnotationAnalysisUnit() as FunctionAnalysisUnit;
            var functionAnnotation = annotationAnalysis?.Function.FunctionDefinition;
            if (functionAnnotation != null && functionAnnotation.Parameters.Length != Ast.Parameters.Length) {
                AnalysisLog.Add("AnnotationParameterCountMismatch", Scope.Name, Scope.OuterScope.Name);
                functionAnnotation = null;
            }

            bool overwriteWithAnnotations = ProjectEntry.ProjectState.Limits.UseTypeStubPackagesExclusively;

            for (var i = 0; i < Ast.Parameters.Length; ++i) {
                var p = Ast.Parameters[i];
                var annotation = p.Annotation;
                IAnalysisSet annotationValue = null;
                if (annotation != null) {
                    annotationValue = ddg._eval.EvaluateAnnotation(annotation);
                } else if (functionAnnotation?.Parameters[i].Annotation != null) {
                    try {
                        ddg.SetCurrentUnit(annotationAnalysis);
                        annotationValue = ddg._eval.EvaluateAnnotation(functionAnnotation.Parameters[i].Annotation);
                    } finally {
                        ddg.SetCurrentUnit(this);
                    }
                }

                foreach(var annotationProvider in ProjectEntry.ProjectState.ParameterAnnotationProviders)
                    annotationValue ??= annotationProvider.GetAnnotation(this, p);

                if (annotationValue?.Any() == true) {
                    AddParameterTypes(p.Name, annotationValue, overwrite: overwriteWithAnnotations);
                }

                if (p.DefaultValue != null && p.Kind != ParameterKind.List && p.Kind != ParameterKind.Dictionary &&
                    Scope.TryGetVariable(p.Name, out param)) {
                    var val = ddg._eval.Evaluate(p.DefaultValue);
                    if (val != null) {
                        AddParameterTypes(p.Name, val, overwrite: false);
                    }
                }
            }

            IAnalysisSet ann = null;
            if (Ast.ReturnAnnotation != null) {
                ann = ddg._eval.EvaluateAnnotation(Ast.ReturnAnnotation);
            } else if (functionAnnotation?.ReturnAnnotation != null) {
                try {
                    ddg.SetCurrentUnit(annotationAnalysis);
                    ann = ddg._eval.EvaluateAnnotation(functionAnnotation.ReturnAnnotation);
                } finally {
                    ddg.SetCurrentUnit(this);
                }
            }
            foreach (var annotationProvider in ProjectEntry.ProjectState.ReturnsAnnotationProviders)
                ann ??= annotationProvider.GetAnnotation(this);

            if (ann != null) {
                var resType = ann;
                if (Ast.IsGenerator) {
                    if (ann.Split<ProtocolInfo>(out var gens, out resType)) {
                        var gen = ((FunctionScope)Scope).Generator;
                        foreach (var g in gens.SelectMany(p => p.GetProtocols<GeneratorProtocol>())) {
                            if (overwriteWithAnnotations) {
                                gen.Yields.SetTypes(ProjectEntry, g.Yielded);
                                gen.Yields.Lock();
                                gen.Sends.SetTypes(ProjectEntry, g.Sent);
                                gen.Sends.Lock();
                                gen.Returns.SetTypes(ProjectEntry, g.Returned);
                                gen.Returns.Lock();
                            } else {
                                gen.Yields.AddTypes(ProjectEntry, g.Yielded);
                                gen.Sends.AddTypes(ProjectEntry, g.Sent);
                                gen.Returns.AddTypes(ProjectEntry, g.Returned);
                            }
                        }
                    }
                } else {
                    if (overwriteWithAnnotations && ann.Count > 0) {
                        ((FunctionScope)Scope).SetReturnTypesAndLock(
                            Ast.ReturnAnnotation,
                            ddg._unit,
                            resType
                        );
                    } else {
                        ((FunctionScope)Scope).AddReturnTypes(
                            Ast.ReturnAnnotation,
                            ddg._unit,
                            resType
                        );
                    }

                    var classInfo = (Function.AnalysisUnit.Scope.OuterScope as ClassScope)?.Class;
                    if (classInfo != null && Function.Name != "__init__" && Function.Name != "__new__") {
                        var linked = classInfo.TraverseTransitivelyLinked(c => c.GetReturnTypePropagationLinks().OfType<ClassInfo>());
                        foreach (ClassInfo linkedClass in linked) {
                            if (!linkedClass.Scope.TryGetVariable(Function.Name, out var linkedFunctionVariable)) continue;
                            var linkedFunction = linkedFunctionVariable.Types as FunctionInfo;
                            var linkedAnalysisUnit = (FunctionAnalysisUnit)linkedFunction?.AnalysisUnit;
                            if (FunctionScope.ReturnValue.IsLocked)
                                linkedAnalysisUnit?.FunctionScope.SetReturnTypesAndLock(Ast.ReturnAnnotation, ddg._unit, resType);
                            else
                                linkedAnalysisUnit?.FunctionScope.AddReturnTypes(Ast.ReturnAnnotation, ddg._unit, resType);
                        }
                    }
                }
            }
        }

        private FunctionScope FunctionScope => (FunctionScope)Scope;

        private bool AddParameterTypes(FunctionScope functionScope, string name, IAnalysisSet types, bool overwrite) {
            bool added = false;
            if (!functionScope.TryGetVariable(name, out var param))
                return added;
            if (overwrite) {
                added |= param.SetTypes(ProjectEntry, types, false);
                param.Lock();
            } else
                added |= param.AddTypes(this, types, false);
            var vd = functionScope.GetParameter(name);
            if (vd != null && vd != param) {
                if (overwrite) {
                    added |= vd.SetTypes(ProjectEntry, types, false);
                    vd.Lock();
                }  else
                    added |= vd.AddTypes(this, types, false);
            }
            return added;
        }

        private bool AddParameterTypes(string name, IAnalysisSet types, bool overwrite) {
            var functionScope = FunctionScope;
            overwrite &= types.Count > 0;
            bool added = AddParameterTypes(functionScope, name, types, overwrite);
            if (!added) {
                return added;
            }

            if (Function.FunctionDefinition.IsLambda || Function.Name == "__init__" || Function.Name == "__new__") {
                return added;
            }

            if (!(Scope.OuterScope is ClassScope classScope)) {
                return added;
            }
            var linked = classScope.Class.TraverseTransitivelyLinked(c => c.GetParameterTypePropagationLinks().OfType<ClassInfo>())
                .Select(c => c.Scope.TryGetVariable(Function.Name, out var linkedFunctionVariable) ? linkedFunctionVariable.Types : null)
                .OfType<FunctionInfo>();
            foreach (FunctionInfo linkedFunction in linked) {
                var linkedFunctionAnalysisUnit = (FunctionAnalysisUnit)linkedFunction.AnalysisUnit;
                AddParameterTypes(linkedFunctionAnalysisUnit?.FunctionScope, name, types, overwrite);
            }

            return added;
        }

        private AnalysisValue[] GetBaseMethods() {
            return Scope?.OuterScope is ClassScope @class
                ? DDG.LookupBaseMethods(Function.Name, @class.Class.Mro, Ast, this).ToArray()
                : Array.Empty<AnalysisValue>();
        }

        public override string ToString() {
            return "{0}{1}({2})->{3}".FormatInvariant(
                base.ToString(),
                " def:",
                string.Join(", ", Ast.Parameters.Select(p => InterpreterScope.TryGetVariable(p.Name, out var v) ? v.Types.ToString() : "{}")),
                ((FunctionScope)Scope).ReturnValue.Types.ToString()
            );
        }
    }

    class FunctionClosureAnalysisUnit : FunctionAnalysisUnit {
        private readonly FunctionAnalysisUnit _originalUnit;
        private readonly IVersioned _agg;

        internal FunctionClosureAnalysisUnit(IVersioned agg, FunctionAnalysisUnit originalUnit, CallChain callChain) :
            base(originalUnit.Function, originalUnit._declUnit, originalUnit._declUnit.InterpreterScope, originalUnit.ProjectEntry, true) {
            _originalUnit = originalUnit;
            _agg = agg;
            CallChain = callChain;
            _originalUnit.InterpreterScope.AddLinkedScope(InterpreterScope);

            var node = originalUnit.Function.FunctionDefinition;
            node.Body.Walk(new OverviewWalker(originalUnit.ProjectEntry, this, originalUnit.Tree));

            AnalysisLog.NewUnit(this);
        }

        public override IVersioned DependencyProject => _agg;
        public FunctionAnalysisUnit OriginalUnit => _originalUnit;
        internal override ILocationResolver AlternateResolver => _originalUnit;

        public CallChain CallChain { get; }

        public override string ToString() {
            return base.ToString() + " " + CallChain.ToString();
        }
    }
}
