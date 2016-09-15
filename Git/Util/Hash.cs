using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Git {

    public struct Hash : IEquatable<Hash> {

        public static implicit operator string(Hash hash) => hash.ToString();

        public static Hash Create(byte[] value) {
            return new Hash(value);
        }
        public static Hash Parse(string value, int length) {
            var pattern = $"([a-fA-F0-9]){{{length}}}";
            if (!Regex.IsMatch(value, $"^{pattern}$"))
                throw new Exception($"Expected regex '{pattern}' to match '{value}'.");
            var bytes = Enumerable.Range(0, value.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
                .ToArray();
            return new Hash(bytes);
        }

        private readonly byte[] m_value;

        private Hash(byte[] value) {
            m_value = value;
        }

        public byte[] Value => m_value;

        public override bool Equals(object obj) => obj is Hash ? ((Hash)obj) == this : false;
        public bool Equals(Hash other) => m_value != null && other.m_value != null && m_value.SequenceEqual(other.m_value);
        public override int GetHashCode() => m_value.Aggregate(0, (a, o) => a ^ o.GetHashCode());
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