namespace Microsoft.PythonTools.Analysis.Values {
    public interface ITypingTypeInfo: IAnalysisValue {
        IBuiltinClassInfo TypingClass { get; }
    }
}
