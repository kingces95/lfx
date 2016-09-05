using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace Git.Lfs {

    public struct LfsHash {
        public const string Pattern = "([a-f][0-9]){64}";

        public static implicit operator string(LfsHash hash) => hash.m_value;

        public static LfsHash Compute(string value) {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);
            sw.Write(value);
            sw.Flush();
            return Compute(ms.GetBuffer(), (int)ms.Position);
        }
        public static LfsHash Compute(Stream stream) {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Compute(ms.GetBuffer(), (int)ms.Position);
        }
        public static LfsHash Compute(byte[] bytes) => bytes.ComputeHash(bytes.Length);
        public static LfsHash Compute(byte[] bytes, int count) {
            var sha = SHA256.Create();
            var hash = new StringBuilder();
            foreach (var b in sha.ComputeHash(bytes, 0, count))
                hash.Append(b.ToString("x2"));
            return new LfsHash(hash.ToString());
        }

        private readonly string m_value;

        public LfsHash(string value) {
            if (!Regex.IsMatch(value, Pattern))
                throw new Exception($"Expected regex '{Pattern}' to match '{value}'.");
            m_value = value;
        }

        public string Value => m_value;
        public override string ToString() => m_value;
    }
}