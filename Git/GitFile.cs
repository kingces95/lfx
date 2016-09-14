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

            var stream = GitCmd.Execute("ls-files");
            foreach (var line in stream.Lines()) {
                var pathUrl = new Uri(dirUrl, line);
                yield return new GitFile(pathUrl.LocalPath);
            }
        }

        private const string AttributeRegexName = "name";
        private const string AttributeRegexValue = "value";
        private static readonly string AttributeRegex =
            $"^[^:]*:*\\s+(?<{AttributeRegexName}>[^:]*):\\s+(?<{AttributeRegexValue}>.*)$";

        private readonly string m_path;
        private readonly string m_pathLower;
        private readonly Dictionary<string, string> m_attributes;

        public GitFile(string path) {
            m_path = path;
            m_pathLower = m_path.ToLower();

            var lines = GitCmd.Execute(
                $"check-attr -a {Path.GetFileName(path)}",
                Path.GetDirectoryName(path).ToDir()
            ).Lines();

            m_attributes = new Dictionary<string, string>();
            foreach (var line in lines) {
                var match = Regex.Match(line, AttributeRegex, RegexOptions.IgnoreCase);
                var key = match.Groups[AttributeRegexName].Value;
                var value = match.Groups[AttributeRegexValue].Value;
                m_attributes[key] = value;
            }
        }

        public bool TryGetValue(string key, out string value) => m_attributes.TryGetValue(key, out value);
        public string GetValue(string key) {
            string value;
            TryGetValue(key, out value);
            return value;
        }
        public bool Contains(string key) => m_attributes.ContainsKey(key);
        public IEnumerable<KeyValuePair<string, string>> Attributes() => m_attributes;

        public bool Equals(GitFile other) => other == null ? false : m_pathLower == other.m_pathLower;
        public override bool Equals(object obj) => Equals(obj as GitFile);
        public override int GetHashCode() => m_pathLower.GetHashCode();
        public override string ToString() => m_path;
    }
}