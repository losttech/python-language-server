using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;
namespace Microsoft.PythonTools.Analysis.Values {
    class SetBuiltInClassInfo : SequenceBuiltinClassInfo {
        public SetBuiltInClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        protected override BuiltinInstanceInfo MakeInstance() {
            return new SequenceBuiltinInstanceInfo(this, false, false);
        }

        internal override SequenceInfo MakeFromIndexes(Node node, IPythonProjectEntry entry) {
            var indexTypes = _indexTypes.Length > 0
                ? _indexTypes.Zip(VariableDef.Generator, (t, v) => { v.AddTypes(entry, t, false, entry); return v; }).ToArray()
                : VariableDef.EmptyArray;
            return new SetInfo(this, node, entry, indexTypes);
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, Type.Name);
            if (_indexTypes == null || _indexTypes.Length == 0) {
                yield break;
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " of ");
            foreach (var kv in AnalysisSet.UnionAll(_indexTypes).GetRichDescriptions()) {
                yield return kv;
            }
        }
    }
}
