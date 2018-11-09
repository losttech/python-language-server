using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    class ReferenceComparer: IEqualityComparer, IEqualityComparer<object> {
        private ReferenceComparer() { }

        public static ReferenceComparer Instance { get; } = new ReferenceComparer();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
