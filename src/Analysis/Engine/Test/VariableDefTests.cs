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

using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;
using TestUtilities;

namespace AnalysisTests {


    [TestClass]
    public class VariableDefTests: ServerBasedTest {
        [TestMethod]
        public async Task AddTypes_Is_Symmetric_For_Dict() {
            var code = @"def f(*fob, **oar):
    pass
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var entry = (IPythonProjectEntry)server.GetEntry(analysis.DocumentUri);
                var untypedDict = new DictionaryInfo(entry, new DictionaryExpression());
                var intDict = new DictionaryInfo(entry, new DictionaryExpression());
                var @int = server.Analyzer.GetBuiltinType(server.Analyzer.Types[BuiltinTypeId.Int]);
                intDict.SetIndex(new ConstantExpression(42), analysis.Scope.AnalysisValue.AnalysisUnit, index: @int, value: AnalysisSet.Empty);

                var untypedTyped = new VariableDef();
                untypedTyped.AddTypes(entry, untypedDict);
                untypedTyped.AddTypes(entry, intDict);
                var typedUntyped = new VariableDef();
                typedUntyped.AddTypes(entry, intDict);
                typedUntyped.AddTypes(entry, untypedDict);

                Assert.AreEqual(untypedTyped.Types.ToString(), typedUntyped.Types.ToString());
            }
        }

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }
    }
}
