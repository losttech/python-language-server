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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class CollectionsModuleInfo: BuiltinModule {
        private readonly BuiltinModule _inner;
        private CollectionsModuleInfo(BuiltinModule inner) : base(inner.InterpreterModule, inner.ProjectState) {
            this._inner = inner;
        }


        public static BuiltinModule Wrap(BuiltinModule inner) => new CollectionsModuleInfo(inner);

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {

            switch (name) {
            case "namedtuple":
                return new TypingTypeInfo("NamedTuple", _inner.GetMember(node, unit, "namedtuple")?.FirstOrDefault());
            }

            return _inner.GetMember(node, unit, name);
        }
    }
}
