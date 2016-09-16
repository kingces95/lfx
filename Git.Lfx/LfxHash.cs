using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Git.Lfs {

    public struct LfsHash {
        private const int Length = 64;

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
            return Create(SHA256.Create().ComputeHash(stream));
        }

        public static LfsHash Create(byte[] value) => new LfsHash(Hash.Create(value));
        public static LfsHash Parse(string value) => new LfsHash(Hash.Parse(value, Length));

        private readonly Hash m_value;

        private LfsHash(Hash value) {
            m_value = value;
        }

        public byte[] Value => m_value.Value;

        public override bool Equals(object obj) => obj is LfsHash ? ((LfsHash)obj).m_value == m_value : false;
        public bool Equals(LfsHash other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }
}