#nullable enable
namespace Microsoft.PythonTools.Analysis {
    public interface IReturnsAnnotationProvider {
        IAnalysisSet? GetAnnotation(AnalysisUnit analysisUnit);
    }
}
