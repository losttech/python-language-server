#nullable enable
namespace Microsoft.PythonTools.Analysis {
    using Microsoft.PythonTools.Parsing.Ast;

    public interface IParameterAnnotationProvider {
        IAnalysisSet? GetAnnotation(AnalysisUnit analysisUnit, Parameter parameter);
    }
}
