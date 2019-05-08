namespace Microsoft.PythonTools.Analysis.Values {
    public interface IKeyValueInfo {
        IAnalysisSet Key { get; }
        IAnalysisSet Value { get; }
    }
}
