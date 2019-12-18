namespace Microsoft.PythonTools.Analysis.Values {
    public interface IPropertyInfo {
        IFunctionInfo2 Getter { get; }
        IFunctionInfo2 Setter { get; }
    }
}
