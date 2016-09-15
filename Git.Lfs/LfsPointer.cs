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
        private const string ArchiveHintKey = "archiveHint";
        private const string OidHashMethodSha256 = "sha256";

        public static LfsPointer Parse(TextReader stream) => Parse(stream.ReadToEnd());
        public static bool CanParse(TextReader stream) {
            LfsPointer pointer;
            return TryParse(stream, out pointer);
        }
        public static bool TryParse(TextReader stream, out LfsPointer pointer) {
            string errorMessage;
            return TryParse(stream, out pointer, out errorMessage);
        }
        public static bool TryParse(TextReader stream, out LfsPointer pointer, out string errorMessage) {
            pointer = new LfsPointer();
            errorMessage = null;

            var lines = stream.Lines(EndOfLine, 4096);

            var last = false;
            var first = true;
            var previousKey = string.Empty;
            foreach (var line in lines) {

                if (line == string.Empty) {
                    last = true;
                    continue;
                }

                if (string.IsNullOrEmpty(line)) {
                    errorMessage = "Unexpected blank line encountered in lfs pointer.";
                    return false;
                }

                var indexOfSpace = line.IndexOf(' ');
                if (indexOfSpace == -1) {
                    errorMessage = $"Expected space separating pointer line '{line}'.";
                    return false;
                }

                var key = line.Substring(0, indexOfSpace);
                var value = line.Substring(indexOfSpace + 1);

                if (first && key != VersionKey) {
                    errorMessage = $"Expected first lfs pointer key to be '{VersionKey}' but was '{key}'.";
                    return false;
                }
                first = false;

                if (key.CompareTo(previousKey) <= 0) {
                    errorMessage = $"Expected lfs pointer '{key}' to come before previous key '{previousKey}'.";
                    return false;
                }

                pointer.Add(key, value);
            }

            if (!last) {
                errorMessage = "Expected lfs pointer to end in '\\n\\n'.";
                return false;
            }

            return true;
        }

        public static LfsPointer Parse(string text) {
            string errorMessage;
            LfsPointer pointer;
            if (!TryParse(text, out pointer, out errorMessage))
                throw new Exception(errorMessage);
            return pointer;
        }
        public static bool CanParse(string text) {
            LfsPointer pointer;
            return TryParse(text, out pointer);
        }
        public static bool TryParse(string text, out LfsPointer pointer) {
            string errorMessage;
            return TryParse(text, out pointer, out errorMessage);
        }
        public static bool TryParse(string text, out LfsPointer pointer, out string errorMessage) {
            return TryParse(new StringReader(text), out pointer, out errorMessage);
        }

        public static bool TryLoad(string path, out LfsPointer pointer) {
            using (var stream = new StreamReader(path))
                return TryParse(stream, out pointer);
        }
        public static LfsPointer Load(string path) => Parse(File.ReadAllText(path));

        public static LfsPointer Create(string path) {
            LfsPointer pointer;
            using (var file = File.OpenRead(path))
                pointer = Create(file);

            var config = LfsConfig.Load(path);

            // simple
            if (config.Type == LfsPointerType.Simple)
                return pointer;

            var url = config.Url;
            var relPath = string.Empty;
            if (config.HasPattern) {
                relPath = config.Pattern.ConfigFile.Path.GetDir().ToUrl().MakeRelativeUri(path.ToUrl()).ToString();
                url = Regex.Replace(
                    input: relPath,
                    pattern: config.Pattern,
                    replacement: config.Url
                );
            }

            // curl
            if (config.Type == LfsPointerType.Curl)
                return pointer.AddUrl(url.ToUrl());

            var archiveHint = config.ArchiveHint.Value;
            if (config.HasPattern) {
                archiveHint = Regex.Replace(
                    input: relPath,
                    pattern: config.Pattern,
                    replacement: config.ArchiveHint
                );
            }

            // archive
            return pointer.AddArchive(url.ToUrl(), archiveHint);
        }
        public static LfsPointer Create(byte[] bytes, int? count = null) {
            return Create(new MemoryStream(bytes, 0, count ?? bytes.Length));
        }
        public static LfsPointer Create(Stream stream) {
            stream = new StreamCounter(stream);
            var hash = LfsHash.Compute(stream);
            var count = stream.Position;

            return Create(hash, count);
        }
        public static LfsPointer Create(LfsHash hash, long count) {
            var pointer = new LfsPointer();
            pointer.Add(VersionKey, Version1Uri.ToString());
            pointer.Add(SizeKey, $"{count}");
            pointer.Add(OidKey, $"{OidHashMethodSha256}:{hash}");
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
        private const string KeyRegex = "^([a-z|A-Z|0-9|.|-])*$";
        private const string ValueRegex = "^([^\n\r])*$";
        private static readonly Dictionary<string, string> KnownKeyRegex =
            new Dictionary<string, string>() {
                [OidKey] = $"^{OidHashMethodSha256}" + ":([a-fA-F0-9]){64}$",
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
        public string ArchiveHint => this[ArchiveHintKey];

        public LfsPointer AddUrl(Uri url) {

            if (Type != LfsPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfsPointer(LfsPointerType.Curl, Pairs);
            pointer.Add(UrlKey, $"{url}");
            return pointer;
        }
        public LfsPointer AddArchive(Uri url, string hint) {

            if (Type != LfsPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfsPointer(LfsPointerType.Archive, Pairs);
            pointer.Add(ArchiveHintKey, $"{hint}");
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
