using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;

namespace Git {

    public enum GitNamedConfigFile {
        System,
        Global,
        Local,
        File,
    }

    public abstract class GitConfigFile : IEnumerable<GitConfigValue> {
        internal const string GitDirName = ".git";

        private sealed class User : GitConfigFile {
            private readonly string m_path;

            internal User(string path) {
                m_path = path;
            }

            internal override string RefreshCommand => $"config -l -f {m_path}";

            public override GitConfigFile Reload() => new User(m_path);
            public override string Path => m_path;
            public override string ToString() => m_path;
        }
        private sealed class WellKnown : GitConfigFile {
            private const string GitLocalConfigFileName = "config";
            private static readonly Dictionary<GitNamedConfigFile, string> FileSpec =
                new Dictionary<GitNamedConfigFile, string>() {
                    [GitNamedConfigFile.Local] = "--local",
                    [GitNamedConfigFile.Global] = "--global",
                    [GitNamedConfigFile.System] = "--system"
                };

            internal static GitConfigFile Load(GitNamedConfigFile name, string workingDir = null) {
                if (name == GitNamedConfigFile.System)
                    return new WellKnown(GitNamedConfigFile.System);

                if (name == GitNamedConfigFile.Global)
                    return new WellKnown(GitNamedConfigFile.Global);

                if (workingDir == null)
                    workingDir = Environment.CurrentDirectory;
                workingDir = workingDir.ToDir();

                var gitDir = workingDir.FindFileAbove(GitDirName, directory: true);
                if (gitDir == null)
                    return null;
                var path = gitDir + GitLocalConfigFileName;

                return new WellKnown(GitNamedConfigFile.Local, workingDir, path);
            }

            private readonly string m_path;
            private readonly string m_workingDir;
            private readonly GitNamedConfigFile m_name;

            internal WellKnown(GitNamedConfigFile name, string workingDir = null, string path = null) {
                m_name = name;
                m_workingDir = workingDir;
                m_path = path;
            }

            internal override string RefreshCommand => $"config -l {FileSpec[m_name]}";

            public override string Path => m_path;
            public override GitConfigFile Reload() => new WellKnown(m_name, m_workingDir);
            public override GitNamedConfigFile Name => m_name;
            public override string WorkingDir => m_workingDir;

            public override string ToString() {
                if (Name == GitNamedConfigFile.File)
                    return Path;
                return Name.ToString();
            }
        }

        private const string ConfigRegexName = "name";
        private const string ConfigRegexValue = "value";
        private static readonly string ConfigRegex =
            $"(?<{ConfigRegexName}>[^=]*)=(?<{ConfigRegexValue}>.*)";

        public static GitConfigFile LoadSystem() => WellKnown.Load(GitNamedConfigFile.System);
        public static GitConfigFile LoadGlobal() => WellKnown.Load(GitNamedConfigFile.Global);
        public static GitConfigFile Load(string path = null) {
            if (path != null && !path.IsDir()) {
                if (!File.Exists(path))
                    throw new Exception($"Expected file to exist at '{path}'.");

                return new User(path);
            }

            var workingDir = path;
            if (workingDir == null)
                workingDir = Environment.CurrentDirectory;
            workingDir = workingDir.ToDir();
            return WellKnown.Load(GitNamedConfigFile.Local, workingDir);
        }

        private static readonly Dictionary<string, GitConfigFile> m_files;

        private readonly Lazy<Dictionary<string, GitConfigValue>> m_config;

        private GitConfigFile() {
            m_config = new Lazy<Dictionary<string, GitConfigValue>>(() => {
                var config = new Dictionary<string, GitConfigValue>(
                    StringComparer.InvariantCultureIgnoreCase);

                // Bug: `git config -l --local` inexplicably fails when both 
                // (1) called via git filter and when (2) working dir explictly set
                string workingDir = null; // = WorkingDir;

                var sr = GitCmd.Execute(RefreshCommand, workingDir);

                foreach (var line in sr.Lines()) {
                    var match = Regex.Match(line, ConfigRegex, RegexOptions.IgnoreCase);
                    var key = match.Groups[ConfigRegexName].Value;
                    var value = match.Groups[ConfigRegexValue].Value;
                    config[key] = new GitConfigValue(this, key, value);
                }

                return config;
            });
        }

        internal abstract string RefreshCommand { get; }

        public abstract GitConfigFile Reload();
        public virtual string WorkingDir => null;
        public virtual string Path => null;
        public virtual GitNamedConfigFile Name => GitNamedConfigFile.File;

        public int Count => m_config.Value.Count;
        public bool Contains(string key) {
            string value;
            return TryGetValue(key, out value);
        }
        public string this[string key] => m_config.Value.GetValueOrDefault(key);
        public bool TryGetValue(string key, out string value) {
            value = null;

            GitConfigValue _value;
            if (!TryGetValue(key, out _value))
                return false;

            value = _value.Value;
            return true;
        }
        public bool TryGetValue(string key, out GitConfigValue value) {
            return m_config.Value.TryGetValue(key, out value);
        }

        public IEnumerable<string> Keys() => m_config.Value.Keys.Cast<string>();
        public IEnumerator<GitConfigValue> GetEnumerator() => m_config.Value.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}