using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Git {
    public sealed class GitFile : IEquatable<GitFile> {
        public static IEnumerable<GitFile> Load(string dir = null) {
            if (dir == null)
                dir = Environment.CurrentDirectory;
            dir = dir.ToDir();
            var dirUrl = dir.ToUrl();

            var stream = new StreamReader(
                GitCmd.Stream("check-attr -a --stdin",
                    inputStream: GitCmd.Stream("ls-files")
                )
            );


            Uri currentPath = null;
            Dictionary<string, string> attributes = null;

            foreach (var line in stream.Lines()) {
                var match = Regex.Match(line, AttributeRegex, RegexOptions.IgnoreCase);
                var thisPath = new Uri(dirUrl, match.Groups[PatternPath].Value);
                var key = match.Groups[PatternName].Value;
                var value = match.Groups[PatternValue].Value;
                if (string.IsNullOrEmpty(value))
                    value = null;

                if (currentPath != thisPath) {
                    if (currentPath != null)
                        yield return new GitFile(currentPath.LocalPath, attributes);

                    currentPath = thisPath;
                    attributes = new Dictionary<string, string>(
                        StringComparer.InvariantCultureIgnoreCase);
                }

                attributes[key] = value;
            }
        }

        private const string PatternPath = "path";
        private const string PatternName = "name";
        private const string PatternValue = "value";
        private static readonly string AttributeRegex =
            $"^(?<{PatternPath}>[^:]*):*\\s+(?<{PatternName}>[^:]*):\\s+(?<{PatternValue}>.*)$";

        private readonly string m_path;
        private readonly string m_pathLower;
        private readonly Dictionary<string, string> m_attributes;

        public GitFile(string path, Dictionary<string, string> attributes) {
            m_path = path;
            m_pathLower = m_path.ToLower();
            m_attributes = attributes;
        }

        public string GetAttribute(string key) {
            string value;
            m_attributes.TryGetValue(key, out value);
            return value;
        }
        public bool IsDefined(string key, string value = null) {
            if (value == null)
                return m_attributes.ContainsKey(key);

            return string.Equals(GetAttribute(key), value);
        }
        public IEnumerable<KeyValuePair<string, string>> Attributes() => m_attributes;

        public bool Equals(GitFile other) => other == null ? false : m_pathLower == other.m_pathLower;
        public override bool Equals(object obj) => Equals(obj as GitFile);
        public override int GetHashCode() => m_pathLower.GetHashCode();
        public override string ToString() => m_path;
    }
}