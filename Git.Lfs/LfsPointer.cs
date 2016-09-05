using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Immutable;

namespace Git.Lfs
{
    public enum LfsPointerType {
        Simple,
        Curl,
        Archive
    }
    public enum LfsHashMethod {
        Sha256 = 1
    }
    public struct LfsPointer : IEquatable<LfsPointer> {
        public static readonly Uri PreReleaseVersionUri = "https://hawser.github.com/spec/v1".ToUrl();
        public static readonly Uri Version1Uri = "https://git-lfs.github.com/spec/v1".ToUrl();

        private const string VersionKey = "version";
        private const string OidKey = "oid";
        private const string SizeKey = "size";
        private const string UrlKey = "url";
        private const string TypeKey = "type";
        private const string HintKey = "hint";
        private const string OidHashMethodSha256 = "sha256";

        public static LfsPointer Parse(TextReader stream) => Parse(stream.ReadToEnd());
        public static LfsPointer Parse(string text) {

            if (!text.EndsWith(EndOfPointer))
                throw new Exception("Expected lfs pointer to end in '\\n\\n'.");

            text = text.Substring(0, text.Length - EndOfPointer.Length);
            var lines = text.Split(EndOfLine[0]);

            var pointer = new LfsPointer();

            var first = false;
            var previousKey = string.Empty;
            foreach (var line in lines) {
                if (string.IsNullOrEmpty(line))
                    throw new Exception("Unexpected blank line encountered in lfs pointer.");

                var indexOfSpace = line.IndexOf(' ');
                if (indexOfSpace == -1)
                    throw new Exception($"Expected space separating pointer line '{line}'.");

                var key = line.Substring(0, indexOfSpace);
                var value = line.Substring(indexOfSpace + 1);

                if (first && key != VersionKey)
                    throw new Exception($"Expected first lfs pointer key to be '{VersionKey}' but was '{key}'.");
                first = false;

                if (key.CompareTo(previousKey) <= 0)
                    throw new Exception($"Expected lfs pointer '{key}' to come before previous key '{previousKey}'.");

                pointer.Add(key, value);
            }

            return pointer;
        }
        public static LfsPointer Load(string path) => Parse(File.ReadAllText(path));

        public static LfsPointer Create(string path) {
            using (var file = File.OpenRead(path))
                return Create(file);
        }
        public static LfsPointer Create(Stream stream) {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Create(ms.GetBuffer(), (int)ms.Position);
        }
        public static LfsPointer Create(byte[] bytes, int? count = null) {
            if (count == null)
                count = bytes.Length;

            var pointer = new LfsPointer();
            pointer.Add(VersionKey, Version1Uri.ToString());
            pointer.Add(SizeKey, $"{count}");
            pointer.Add(OidKey, $"{OidHashMethodSha256}:{bytes.ComputeHash((int)count)}");
            return pointer;
        }
        private static void Verify(string key, string value) {
            if (!Regex.IsMatch(key, KeyRegex))
                throw new Exception($"Key '{key}' does not match regex '{KeyRegex}'.");

            string valueRegex;
            if (!KnownKeyRegex.TryGetValue(key, out valueRegex))
                valueRegex = ValueRegex;
            if (!Regex.IsMatch(value, valueRegex))
                throw new Exception($"Value '{value}' does not match regex '{valueRegex}'.");

            if (UrlKeys.Contains(key))
                value.ToUrl();
        }

        private const string EndOfLine = "\n";
        private const string EndOfPointer = EndOfLine;
        private const string KeyRegex = "^([a-z|0-9|.|-])*$";
        private const string ValueRegex = "^([^\n\r])*$";
        private static readonly Dictionary<string, string> KnownKeyRegex =
            new Dictionary<string, string>() {
                [OidKey] = $"^{OidHashMethodSha256}:{LfsHash.Pattern}$",
                [SizeKey] = @"^(\d*)$"
            };
        private static readonly string[] UrlKeys = new[] {
            VersionKey,
            UrlKey,
        };
        private static readonly string[] RequiredKeys = new[] {
            VersionKey,
            OidKey,
            SizeKey
        };

        private ImmutableDictionary<string, string> m__pairs;

        private LfsPointer(LfsPointerType type, ImmutableDictionary<string, string> pairs) {
            m__pairs = pairs.Add(TypeKey, type.ToString().ToLower());
        }

        private ImmutableDictionary<string, string> Pairs {
            get { return m__pairs ?? ImmutableDictionary<string, string>.Empty; }
            set { m__pairs = value; }
        }
        private void Add(string key, string value) {
            Verify(key, value);
            Pairs = Pairs.Add(key, value);
        }
        private string Oid => this[OidKey];

        public LfsPointerType Type => this[TypeKey] == null ? LfsPointerType.Simple : (LfsPointerType)
            Enum.Parse(typeof(LfsPointerType), this[TypeKey], ignoreCase: true);
        public string this[string key] => Pairs.ContainsKey(key) ? Pairs[key] : null;
        public int Size => int.Parse(this[SizeKey]);
        public LfsHash Hash => LfsHash.Parse(HashValue);
        public string HashValue => Oid.Substring(Oid.IndexOf(":") + 1);
        public LfsHashMethod HashMethod => (LfsHashMethod)
            Enum.Parse(typeof(LfsHashMethod), Oid.Substring(0, Oid.IndexOf(":")), ignoreCase: true);
        public Uri Version => this[VersionKey].ToUrl(); 
        public Uri Url => this[UrlKey] == null ? null : this[UrlKey].ToUrl();
        public string Hint => this[HintKey];

        public LfsPointer AddUrl(Uri url) {

            if (Type != LfsPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfsPointer(LfsPointerType.Curl, Pairs);
            pointer.Add(UrlKey, $"{url}");
            return pointer;
        }
        public LfsPointer AddArchive(
            Uri url,
            string hint) {

            if (Type != LfsPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfsPointer(LfsPointerType.Archive, Pairs);
            pointer.Add(HintKey, $"{hint}");
            pointer.Add(UrlKey, $"{url}");
            return pointer;
        }

        public override bool Equals(object obj) => obj is LfsPointer ? Equals((LfsPointer)obj) : false;
        public bool Equals(LfsPointer other) => 
            Pairs.OrderBy(o => o.Key).SequenceEqual(other.Pairs.OrderBy(o => o.Key));
        public override int GetHashCode() => 
            Pairs.OrderBy(o => o.Key).Aggregate(0, (a, o) => a ^ o.Value.GetHashCode());
        public override string ToString() {
            var sb = new StringBuilder();

            var pairs = Pairs
                .OrderByDescending(o => o.Key == VersionKey)
                .ThenBy(o => o.Key);

            foreach (var pair in pairs) {
                var key = pair.Key;
                var value = pair.Value;

                sb.Append($"{key} {value}\n");
            }

            var result = sb.ToString();
            return result;
        }
    }
}
