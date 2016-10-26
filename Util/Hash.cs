using System;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace Util {

    public struct Hash : IEquatable<Hash> {
        public static implicit operator string(Hash hash) => hash.ToString();

        public static Hash Create(byte[] value) {
            return new Hash(value);
        }
        public static Hash Parse(string value, int length) {
            Hash hash;
            if (!TryParse(value, length, out hash))
                throw new Exception($"Expected '{value}' to match '([a-fA-F0-9]){{{length}}}'.");
            return hash;
        }
        public static bool TryParse(string value, int length, out Hash hash) {
            hash = default(Hash);

            var pattern = $"([a-fA-F0-9]){{{length}}}";
            if (!Regex.IsMatch(value, $"^{pattern}$"))
                return false;
            var bytes = Enumerable.Range(0, value.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
                .ToArray();

            hash = new Hash(bytes);
            return true;
        }

        private readonly byte[] m_value;

        private Hash(byte[] value) {
            m_value = value;
        }

        public byte[] Value => m_value;
        public bool IsNull => m_value == null;

        public override bool Equals(object obj) => obj is Hash ? ((Hash)obj) == this : false;
        public bool Equals(Hash other) {
            if (m_value == null && other.m_value == null)
                return true;

            if (m_value == null || other.m_value == null)
                return false;

            return m_value.SequenceEqual(other.m_value);
        }
        public override int GetHashCode() {
            if (m_value == null)
                return 0;

            return m_value.Aggregate(0, (a, o) => a ^ o.GetHashCode());
        }
        public override string ToString() {
            if (m_value == null)
                return "[null]";

            var hash = new StringBuilder();
            foreach (var b in m_value)
                hash.Append(b.ToString("x2"));
            return hash.ToString();
        }
    }
}