namespace Microsoft.PythonTools.Analysis.Values {
    using System.Collections.Generic;

    public interface IProtocolInfo {
        IEnumerable<IAnalysisValue> Protocols { get; }
    }
}
