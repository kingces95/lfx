using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Git.Lfs {

    public struct LfsHash : IEquatable<LfsHash> {
        public const string Pattern = "([a-f|A-Z|0-9]){64}";

        public static implicit operator string(LfsHash hash) => hash.ToString();

        public static LfsHash Compute(string value, Encoding encoding) {
            var ms = new MemoryStream();
            {
                var sw = new StreamWriter(ms, Encoding.UTF8);
                sw.Write(value);
                sw.Flush();
            }
            ms.Capacity = (int)ms.Position;
            ms.Position = 0;

            return Compute(ms);
        }
        public static LfsHash Compute(string path) {
            using (var file = File.OpenRead(path))
                return Compute(file);
        }
        public static LfsHash Compute(byte[] bytes, int? count = null) {
            if (count == null)
                count = bytes.Length;
            return Compute(new MemoryStream(bytes, 0, (int)count));
        }
        public static LfsHash Compute(Stream stream) {
            return new LfsHash(SHA256.Create().ComputeHash(stream));
        }

        public static LfsHash Parse(string value) {
            if (!Regex.IsMatch(value, $"^{Pattern}$"))
                throw new Exception($"Expected regex '{Pattern}' to match '{value}'.");
            var bytes = Enumerable.Range(0, value.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
                .ToArray();
            return new LfsHash(bytes);
        }

        private readonly byte[] m_value;

        private LfsHash(byte[] value) {
            m_value = value;
        }

        public byte[] Value => m_value;

        public override bool Equals(object obj) => obj is LfsHash ? ((LfsHash)obj) == this : false;
        public bool Equals(LfsHash other) => m_value != null && other.m_value != null && m_value.SequenceEqual(other.m_value);
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