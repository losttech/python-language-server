namespace Microsoft.PythonTools.Analysis.Values {
    internal class PropertyInfo: IPropertyInfo {
        public FunctionInfo Getter { get; internal set; }
        public FunctionInfo Setter { get; internal set; }
        IFunctionInfo2 IPropertyInfo.Getter => this.Getter;
        IFunctionInfo2 IPropertyInfo.Setter => this.Setter;
    }
}
