namespace Microsoft.PythonTools.Analysis.Values {
    public static class FunctionInfoExtensions {
        public static bool IsGetter(this IFunctionInfo2 functionInfo)
            => ReferenceEquals(functionInfo.Property?.Getter, functionInfo);
        public static bool IsSetter(this IFunctionInfo2 functionInfo)
            => ReferenceEquals(functionInfo.Property?.Setter, functionInfo);
    }
}
