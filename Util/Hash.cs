using System;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;

namespace Util {

    public static partial class Extensions {

        // ByteVector
        public static ByteVector ToByteVector(this string value, Encoding encoding) => ByteVector.Create(value, encoding);
        public static ByteVector ToByteVector(this Stream stream) => ByteVector.Create(stream);
        public static ByteVector ToByteVector(this byte[] value) => ByteVector.Create(value);
    }

    public struct Sha256Hash : IEquatable<Sha256Hash> {
        public const int Length = 32;
        public const int Digits = Length * 2;

        public static bool operator ==(Sha256Hash lhs, Sha256Hash rhs) => lhs.Equals(rhs);
        public static bool operator !=(Sha256Hash lhs, Sha256Hash rhs) => !lhs.Equals(rhs);
        public static implicit operator ByteVector(Sha256Hash hash) => hash.m_value;
        public static explicit operator Sha256Hash(ByteVector vector) => new Sha256Hash(vector);

        public static implicit operator string(Sha256Hash hash) => hash.ToString();

        private readonly ByteVector m_value;

        private Sha256Hash(ByteVector value) {
            if (value.Length != Length)
                throw new ArgumentException(
                    $"ByteVector of length '{value.Length}' cannot be Sha256 hash..");
            m_value = value;
        }

        public byte this[int index] => m_value[index];

        public override bool Equals(object obj) => obj is Sha256Hash ? ((Sha256Hash)obj) == this : false;
        public bool Equals(Sha256Hash other) => m_value.Equals(other);
        public override int GetHashCode() => m_value.GetHashCode();
        public override string ToString() => m_value.ToString();
    }

    public struct ByteVector : IEquatable<ByteVector> {
        public static implicit operator byte[](ByteVector vector) => vector.m_value;
        public static explicit operator ByteVector(byte[] array) => new ByteVector(array);
        public static bool operator ==(ByteVector lhs, ByteVector rhs) => lhs.Equals(rhs);
        public static bool operator !=(ByteVector lhs, ByteVector rhs) => !lhs.Equals(rhs);

        public static ByteVector Create(string value, Encoding encoding) {
            using (var ms = new MemoryStream()) {
                using (var sw = new StreamWriter(ms, encoding)) {
                    sw.Write(value);
                    sw.Flush();

                    ms.Capacity = (int)ms.Position;
                    ms.Position = 0;

                    return Create(ms);
                }
            }
        }
        public static ByteVector Create(byte[] value) {
            return new ByteVector(value);
        }
        public static ByteVector Create(Stream stream) {
            return new ByteVector(SHA256.Create().ComputeHash(stream));
        }
        public static ByteVector Parse(string value) {
            ByteVector hash;
            if (!TryParse(value, out hash))
                throw new Exception($"Expected '{value}' to match '([a-fA-F0-9])*'.");
            return hash;
        }
        public static bool TryParse(string value, out ByteVector vector) {
            vector = default(ByteVector);

            var pattern = $"([a-fA-F0-9])*";
            if (!Regex.IsMatch(value, $"^{pattern}$"))
                return false;
            var bytes = Enumerable.Range(0, value.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
                .ToArray();

            vector = new ByteVector(bytes);
            return true;
        }
        public static ByteVector Load(string path) {
            using (var sr = new StreamReader(path))
                return Parse(sr.ReadToEnd());
        }

        private readonly byte[] m_value;

        private ByteVector(byte[] value) {
            m_value = value;
        }

        public byte this[int index] => m_value[index];
        public int Length => m_value.Length;

        public override bool Equals(object obj) => obj is ByteVector ? ((ByteVector)obj) == this : false;
        public bool Equals(ByteVector other) {
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