using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Git {

    public struct GitHash : IEquatable<GitHash> {
        private const int Length = 40;

        public static implicit operator string(GitHash hash) => hash.ToString();

        public static GitHash Create(byte[] value) => new GitHash(Hash.Create(value));
        public static GitHash Parse(string value) => new GitHash(Hash.Parse(value, Length));

        private readonly Hash m_value;

        private GitHash(Hash value) {
            m_value = value;
        }

        public byte[] Value => m_value.Value;

        public override bool Equals(object obj) => obj is GitHash ? ((GitHash)obj).m_value == m_value : false;
        public bool Equals(GitHash other) => m_value != null && m_value == other.m_value;
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }
}