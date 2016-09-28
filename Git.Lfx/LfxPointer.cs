using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Git.Lfx
{
    public enum LfxPointerType {
        Simple,
        Curl,
        Archive,
        SelfExtractingArchive
    }
    public enum LfxHashMethod {
        Sha256 = 1
    }
    public struct LfxPointer : IEquatable<LfxPointer> {
        public static readonly Uri PreReleaseVersionUri = "https://hawser.github.com/spec/v1".ToUrl();
        public static readonly Uri Version1Uri = "https://git-lfx.github.com/spec/v1".ToUrl();

        private const string VersionKey = "version";
        private const string OidKey = "oid";
        private const string SizeKey = "size";
        private const string UrlKey = "url";
        private const string ArgsKey = "args";
        private const string TypeKey = "type";
        private const string ArchiveHintKey = "hint";
        private const string OidHashMethodSha256 = "sha256";

        public static LfxPointer Parse(TextReader stream) => Parse(stream.ReadToEnd());
        public static bool CanParse(TextReader stream) {
            LfxPointer pointer;
            return TryParse(stream, out pointer);
        }
        public static bool TryParse(TextReader stream, out LfxPointer pointer) {
            string errorMessage;
            return TryParse(stream, out pointer, out errorMessage);
        }
        public static bool TryParse(TextReader stream, out LfxPointer pointer, out string errorMessage) {
            pointer = new LfxPointer();
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
                    errorMessage = "Unexpected blank line encountered in lfx pointer.";
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
                    errorMessage = $"Expected first lfx pointer key to be '{VersionKey}' but was '{key}'.";
                    return false;
                }
                first = false;

                if (key.CompareTo(previousKey) <= 0) {
                    errorMessage = $"Expected lfx pointer '{key}' to come before previous key '{previousKey}'.";
                    return false;
                }

                pointer.Add(key, value);
            }

            if (first) {
                errorMessage = "Expected lfx poitner to contain version key.";
                return false;
            }

            if (!last) {
                errorMessage = "Expected lfx pointer to end in '\\n\\n'.";
                return false;
            }

            return true;
        }

        public static LfxPointer Parse(string text) {
            string errorMessage;
            LfxPointer pointer;
            if (!TryParse(text, out pointer, out errorMessage))
                throw new Exception(errorMessage);
            return pointer;
        }
        public static bool CanParse(string text) {
            LfxPointer pointer;
            return TryParse(text, out pointer);
        }
        public static bool TryParse(string text, out LfxPointer pointer) {
            string errorMessage;
            return TryParse(text, out pointer, out errorMessage);
        }
        public static bool TryParse(string text, out LfxPointer pointer, out string errorMessage) {
            return TryParse(new StringReader(text), out pointer, out errorMessage);
        }

        public static bool CanLoad(string path) {
            LfxPointer pointer;
            return TryLoad(path, out pointer);
        }
        public static bool TryLoad(string path, out LfxPointer pointer) {
            using (var stream = new StreamReader(path))
                return TryParse(stream, out pointer);
        }
        public static LfxPointer Load(string path) => Parse(File.ReadAllText(path));

        public static LfxPointer Create(string path, LfxHash? hash = null) {
            LfxPointer pointer;

            var length = new FileInfo(path).Length;
            if (hash == null)
                hash = LfxHash.Compute(path);
            pointer = Create(hash.Value, length);

            var config = LfxConfig.Load(path);

            // simple
            if (config.Type == LfxPointerType.Simple)
                return pointer;

            var url = config.Url;
            var relPath = config.ConfigFile.Path.MakeRelativePath(path);
            if (config.HasPattern) {
                // change base of relPath to config file containing pattern
                relPath = config.Pattern.ConfigFile.Path.MakeRelativePath(path);
                url = Regex.Replace(
                    input: relPath,
                    pattern: config.Pattern,
                    replacement: config.Url
                );
            }

            // curl
            if (config.Type == LfxPointerType.Curl)
                return pointer.AddUrl(url.ToUrl());

            // default hint is relPath
            var hint = relPath;
            if (config.HasHint) {

                // hardcoded hint (very rare)
                hint = config.Hint.Value;
                if (config.HasPattern) {

                    // substituted hint (nuget case)
                    hint = Regex.Replace(
                        input: relPath,
                        pattern: config.Pattern,
                        replacement: config.Hint
                    );
                }
            }

            // archive
            if (config.Type == LfxPointerType.Archive)
                return pointer.AddArchive(url.ToUrl(), hint);

            // self extracting archive
            return pointer.AddSelfExtractingArchive(url.ToUrl(), hint, config.Args);
        }
        public static LfxPointer Create(byte[] bytes, int? count = null) {
            return Create(new MemoryStream(bytes, 0, count ?? bytes.Length));
        }
        public static LfxPointer Create(Stream stream) {
            stream = new StreamCounter(stream);
            var hash = LfxHash.Compute(stream);
            var count = stream.Position;

            return Create(hash, count);
        }
        public static LfxPointer Create(LfxHash hash, long count) {
            var pointer = new LfxPointer();
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
        private const string KeyRegex = "^([a-z|0-9|.|-])*$";
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

        private LfxPointer(LfxPointerType type, ImmutableDictionary<string, string> pairs) {
            m__pairs = pairs.Add(TypeKey, type.ToString().ToLowerFirst());
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

        public LfxPointerType Type => this[TypeKey] == null ? LfxPointerType.Simple : (LfxPointerType)
            Enum.Parse(typeof(LfxPointerType), this[TypeKey], ignoreCase: true);
        public string this[string key] => Pairs.ContainsKey(key) ? Pairs[key] : null;
        public int Size => int.Parse(this[SizeKey]);
        public LfxHash Hash => LfxHash.Parse(HashValue);
        public string HashValue => Oid.Substring(Oid.IndexOf(":") + 1);
        public LfxHashMethod HashMethod => (LfxHashMethod)
            Enum.Parse(typeof(LfxHashMethod), Oid.Substring(0, Oid.IndexOf(":")), ignoreCase: true);
        public Uri Version => this[VersionKey].ToUrl(); 
        public Uri Url => this[UrlKey] == null ? null : this[UrlKey].ToUrl();
        public string Args => this[ArgsKey];
        public string ArchiveHint => this[ArchiveHintKey];

        public LfxPointer AddUrl(Uri url) {

            if (Type != LfxPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfxPointer(LfxPointerType.Curl, Pairs);
            pointer.Add(UrlKey, $"{url}");
            return pointer;
        }
        public LfxPointer AddArchive(Uri url, string hint) {

            if (Type != LfxPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfxPointer(LfxPointerType.Archive, Pairs);
            pointer.Add(ArchiveHintKey, $"{hint}");
            pointer.Add(UrlKey, $"{url}");
            return pointer;
        }
        public LfxPointer AddSelfExtractingArchive(Uri url, string hint, string args) {

            if (Type != LfxPointerType.Simple)
                throw new InvalidOperationException();

            var pointer = new LfxPointer(LfxPointerType.SelfExtractingArchive, Pairs);
            pointer.Add(ArchiveHintKey, $"{hint}");
            pointer.Add(UrlKey, $"{url}");
            pointer.Add(ArgsKey, $"{args}");
            return pointer;
        }

        public void Save(string path) {
            using (var sw = new StreamWriter(path))
                sw.Write(ToString());
        }

        public override bool Equals(object obj) => obj is LfxPointer ? Equals((LfxPointer)obj) : false;
        public bool Equals(LfxPointer other) => 
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
