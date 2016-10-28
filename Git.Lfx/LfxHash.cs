using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Util;

namespace Git.Lfx {

    public struct LfxHash : IEquatable<LfxHash> {
        private const int Length = 64;

        public static bool operator ==(LfxHash lhs, LfxHash rhs) => lhs.Equals(rhs);
        public static bool operator !=(LfxHash lhs, LfxHash rhs) => !lhs.Equals(rhs);
        public static implicit operator string(LfxHash hash) => hash.ToString();

        public static LfxHash Create(string value, Encoding encoding) {
            var ms = new MemoryStream();
            {
                var sw = new StreamWriter(ms, Encoding.UTF8);
                sw.Write(value);
                sw.Flush();
            }
            ms.Capacity = (int)ms.Position;
            ms.Position = 0;

            return Create(ms);
        }
        public static LfxHash Create(string path) {
            using (var file = File.OpenRead(path))
                return Create(file);
        }
        public static LfxHash Create(Stream stream) {
            return Create(SHA256.Create().ComputeHash(stream));
        }
        public static LfxHash Create(byte[] value) => new LfxHash(Hash.Create(value));

        public static LfxHash Parse(string value) => new LfxHash(Hash.Parse(value, Length));
        public static bool TryParse(string value, out LfxHash hash) {
            Hash rawHash;
            if (!Hash.TryParse(value, Length, out rawHash))
                return false;

            hash = new LfxHash(rawHash);
            return true;
        }

        private readonly Hash m_value;

        private LfxHash(Hash value) {
            m_value = value;
        }

        public byte[] Value => m_value.Value;

        public override bool Equals(object obj) => obj is LfxHash ? ((LfxHash)obj).m_value == m_value : false;
        public bool Equals(LfxHash other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }
}