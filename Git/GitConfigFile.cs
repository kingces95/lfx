using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections;
using System.Diagnostics;

namespace Git {

    public enum GitNamedConfigFile {
        File,
        Local,
        Global,
        System
    }

    public struct GitConfigValue : IEquatable<GitConfigValue> {
        public static implicit operator string(GitConfigValue configValue) => configValue.Value;

        private GitConfigFile m_configFile;
        private KeyValuePair<string, string> m_keyValue;

        internal GitConfigValue(GitConfigFile configFile, string key, string value) {
            m_configFile = configFile;
            m_keyValue = new KeyValuePair<string, string>(key, value);
        }

        public GitConfigFile ConfigFile => m_configFile;
        public KeyValuePair<string, string> Pair => m_keyValue;
        public string Key => Pair.Key;
        public string Value => Pair.Value;

        public override bool Equals(object obj) => 
            obj is GitConfigValue ? Equals((GitConfigValue)obj) : false;
        public bool Equals(GitConfigValue other) => 
            string.Equals(Key, other.Key, StringComparison.CurrentCultureIgnoreCase) && 
            Value == other.Value;
        public override int GetHashCode() => Key.GetHashCode() ^ Value.GetHashCode();
        public override string ToString() => $"{ConfigFile}: {Key}={Value}";
    }

    public sealed class GitConfig : IEnumerable<GitConfigValue> {

        public static GitConfig LoadSystem() => s_system.Reload();
        public static GitConfig LoadGlobal() => s_global.Reload();
        public static GitConfig LoadLocal(string workingDir = null) => 
            Create(GitConfigFile.LoadLocal(workingDir), LoadGlobal());
        public static GitConfig Create(
            GitConfigFile configFile, 
            GitConfig parent = null) => 
                new GitConfig(configFile, parent);

        static GitConfig() {
            s_system = Create(GitConfigFile.LoadSystem());
            s_global = Create(GitConfigFile.LoadGlobal(), s_system);
        }

        private readonly static GitConfig s_global;
        private readonly static GitConfig s_system;

        private readonly GitConfigFile m_file;
        private readonly GitConfig m_parent;
        private GitConfig(
            GitConfigFile file,
            GitConfig parent) {

            m_file = file;
            m_parent = parent;
        }

        public GitConfig Reload() => new GitConfig(m_file.Reload(), m_parent?.Reload());

        public GitConfigFile File => m_file;
        public GitConfig Parent => m_parent;

        public int Count => m_file.Count + m_parent?.Count ?? 0;
        public bool Contains(string key) {
            string value;
            return TryGetValue(key, out value);
        }
        public GitConfigValue this[string key] {
            get {
                string value;
                if (!m_file.TryGetValue(key, out value)) {
                    if (m_parent != null)
                        return m_parent[key];
                }
                return new GitConfigValue(m_file, key, value);
            }
        }
        public bool TryGetValue(string key, out string value) {
            if (!m_file.TryGetValue(key, out value)) {
                if (m_parent == null)
                    return false;
                return m_parent.TryGetValue(key, out value);
            }
            return true;
        }

        public IEnumerable<string> Keys() {
            var result = m_file.Keys();
            if (m_parent != null)
                result = result.Union(m_parent.Keys());
            return result;
        }
        public IEnumerator<GitConfigValue> GetEnumerator() {
            return Keys().Select(o => this[o]).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class GitConfigFile : IEnumerable<GitConfigValue> {
        private const string ConfigRegexName = "name";
        private const string ConfigRegexValue = "value";
        private static readonly string ConfigRegex =
            $"(?<{ConfigRegexName}>[^=]*)=(?<{ConfigRegexValue}>.*)";
        private static readonly Dictionary<GitNamedConfigFile, string> FileSpec =
            new Dictionary<GitNamedConfigFile, string>() {
                [GitNamedConfigFile.Local] = "--local",
                [GitNamedConfigFile.Global] = "--global",
                [GitNamedConfigFile.System] = "--system"
            };

        public static GitConfigFile LoadSystem() => s_system.Reload();
        public static GitConfigFile LoadGlobal() => s_global.Reload();
        public static GitConfigFile LoadLocal(string workingDir) =>
            new GitConfigFile(GitLoader.GetEnlistmentDirectory(workingDir), GitNamedConfigFile.Local);
        public static GitConfigFile Create(string path) => new GitConfigFile(path);

        static GitConfigFile() {
            s_system = new GitConfigFile(name: GitNamedConfigFile.System);
            s_global = new GitConfigFile(name: GitNamedConfigFile.Global);
        }

        private readonly static GitConfigFile s_global;
        private readonly static GitConfigFile s_system;

        private readonly string m_path;
        private readonly GitNamedConfigFile m_name;
        private readonly Lazy<Dictionary<string, GitConfigValue>> m_config;

        private GitConfigFile(
            string path = null,
            GitNamedConfigFile name = GitNamedConfigFile.File) {

            m_path = path;
            m_name = name;
            m_config = new Lazy<Dictionary<string, GitConfigValue>>(() => {
                var config = new Dictionary<string, GitConfigValue>(
                    StringComparer.InvariantCultureIgnoreCase);

                string fileSpec;
                if (!FileSpec.TryGetValue(m_name, out fileSpec))
                    fileSpec = $"-f {path}";
                var sr = GitCmd.Execute($"config -l {fileSpec}", path);

                while (true) {
                    var line = sr.ReadLine();
                    if (line == null)
                        break;

                    var match = Regex.Match(line, ConfigRegex, RegexOptions.IgnoreCase);
                    var key = match.Groups[ConfigRegexName].Value;
                    var value = match.Groups[ConfigRegexValue].Value;
                    config[key] = new GitConfigValue(this, key, value);
                }

                return config;
            });
        }

        public GitConfigFile Reload() => new GitConfigFile(m_path, m_name);
        public int Count => m_config.Value.Count;
        public bool Contains(string key) {
            string value;
            return TryGetValue(key, out value);
        }
        public string this[string key] => m_config.Value[key];
        public bool TryGetValue(string key, out string value) {
            value = null;

            GitConfigValue _value;
            if (!m_config.Value.TryGetValue(key, out _value))
                return false;

            value = _value.Value;
            return true;
        }

        public IEnumerable<string> Keys() => m_config.Value.Keys.Cast<string>();
        public IEnumerator<GitConfigValue> GetEnumerator() => m_config.Value.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() {
            if (m_name == GitNamedConfigFile.File)
                return m_path;
            return m_name.ToString();
        }
    }
}