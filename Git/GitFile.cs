using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;

namespace Git {
    [Flags]
    public enum GitFileFlags {
        Tracked = 1 << 0,
        Untracked = 1 << 1,
        All = Tracked | Untracked
    }

    public sealed class GitFile : IEquatable<GitFile> {

        public static IEnumerable<GitFile> Load(
            string filter = null,
            string dir = null,
            GitFileFlags flags = GitFileFlags.All) {

            if (dir == null)
                dir = Environment.CurrentDirectory;
            dir = IOPath.GetFullPath(dir.ToDir());

            // tracked, untracked files
            var lsFilesCommand = $"ls-files {filter}";
            if ((flags & GitFileFlags.Tracked) != 0)
                lsFilesCommand += " -c";
            if ((flags & GitFileFlags.Untracked) != 0)
                lsFilesCommand += " -o";

            // ls-files | check-attr
            var stream = GitCmd.Stream(lsFilesCommand, dir)
                .PipeTo(GitCmd.Exe, "check-attr -a --stdin", dir);

            // parse output
            Uri currentPath = null;
            Dictionary<string, string> attributes = null;

            var dirUrl = dir.ToUrl();
            foreach (var line in new StreamReader(stream).Lines()) {
                var match = Regex.Match(line, AttributeRegex, RegexOptions.IgnoreCase);
                var path = match.Get(PatternPath);
                var key = match.Get(PatternName);
                var value = match.Get(PatternValue);

                var thisPath = new Uri(dirUrl, path);

                if (string.IsNullOrEmpty(value))
                    value = null;

                if (currentPath != thisPath) {
                    if (currentPath != null)
                        yield return new GitFile(
                            currentPath.LocalPath, flags, attributes);

                    currentPath = thisPath;
                    attributes = new Dictionary<string, string>(
                        StringComparer.InvariantCultureIgnoreCase);
                }

                attributes[key] = value;
            }

            if (currentPath != null)
                yield return new GitFile(
                    currentPath.LocalPath, flags, attributes);
        }

        private const string PatternPath = "path";
        private const string PatternName = "name";
        private const string PatternValue = "value";
        private static readonly string AttributeRegex =
            $"^(?<{PatternPath}>[^:]*?)\"?:\\s+" +
            $"(?<{PatternName}>[^:]*):\\s+" +
            $"(?<{PatternValue}>.*)$";

        private readonly string m_path;
        private readonly string m_pathLower;
        private readonly Dictionary<string, string> m_attributes;
        private readonly GitFileFlags m_flags;

        public GitFile(
            string path, 
            GitFileFlags flags, 
            Dictionary<string, string> attributes) {

            m_path = path;
            m_flags = flags;
            m_pathLower = m_path.ToLower();
            m_attributes = attributes;
        }

        public string Path => m_path;
        public bool IsUntracked => (m_flags & GitFileFlags.Untracked) != 0;
        public bool IsTracked => !IsUntracked;

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