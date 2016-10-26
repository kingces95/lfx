using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System;
using System.IO;
using Util;

namespace Git {

    public sealed class GitConfig : IEnumerable<GitConfigValue> {

        public static GitConfig LoadSystem() => Load(GitConfigFile.LoadSystem());
        public static GitConfig LoadGlobal() => Load(GitConfigFile.LoadGlobal(), LoadSystem());
        public static GitConfig Load(GitConfigFile configFile, GitConfig parent = null) {
            if (configFile == null)
                throw new ArgumentNullException(nameof(configFile));

            return new GitConfig(configFile, parent);
        }
        public static GitConfig Load(string path = null, string configFileName = null) {

            // explicit config file
            if (path != null && !path.IsDir()) {

                var dir = path.GetDir();
                if (string.Compare(Path.GetFileName(path), configFileName, ignoreCase: true) == 0)
                    dir = dir.GetParentDir();

                return Load(
                    GitConfigFile.Load(path),
                    Load(dir, configFileName ?? Path.GetExtension(path))
                );
            }

            // path is directory
            var workingDir = path;
            if (workingDir == null)
                workingDir = Environment.CurrentDirectory;
            workingDir = workingDir.ToDir();

            // inherited config file
            var parentConfigFile = workingDir.FindFileAbove(configFileName);
            if (parentConfigFile != null)
                return Load(parentConfigFile, configFileName);

            // local config file
            var localConfigFile = GitConfigFile.Load(workingDir);
            if (localConfigFile != null)
                return Load(GitConfigFile.Load(workingDir), LoadGlobal());

            // global config file
            return LoadGlobal();
        }

        private readonly GitConfigFile m_configFile;
        private readonly GitConfig m_parent;
        private GitConfig(
            GitConfigFile configFile,
            GitConfig parent) {

            m_configFile = configFile;
            m_parent = parent;
        }

        public GitConfig Reload() => new GitConfig(m_configFile.Reload(), m_parent?.Reload());

        public GitConfigFile GetConfigFile(GitNamedConfigFile name) {
            if (ConfigFile.Name == name)
                return ConfigFile;

            if (Parent == null)
                return null;

            return Parent.GetConfigFile(name);
        }
        public GitConfigFile ConfigFile => m_configFile;
        public GitConfig Parent => m_parent;
        public string EnlistmentDirectory {
            get { return GetConfigFile(GitNamedConfigFile.Local)?.Path.GetParentDir(); }
        }
        public string GitDirectory {
            get {
                if (EnlistmentDirectory == null)
                    return null;
                return Path.Combine(EnlistmentDirectory, GitConfigFile.GitDirName).ToDir();
            }
        }

        public int Count => Keys().Count();
        public bool Contains(string key) {
            string value;
            return TryGetValue(key, out value);
        }
        public string this[string key] {
            get {
                string value;
                if (!TryGetValue(key, out value))
                    return null;
                return value;
            }
        }
        public bool TryGetValue(string key, out string value) {
            value = null;

            GitConfigValue configValue;
            if (!TryGetValue(key, out configValue))
                return false;

            value = configValue.Value;
            return true;
        }
        public bool TryGetValue(string key, out GitConfigValue value) {
            if (!m_configFile.TryGetValue(key, out value)) {
                if (m_parent == null)
                    return false;
                return m_parent.TryGetValue(key, out value);
            }
            return true;
        }
        public GitConfigValue GetValue(string key) {
            GitConfigValue value;
            if (!TryGetValue(key, out value))
                throw new Exception($"Expected '{key}' config file value not found.");
            return value;
        }

        public IEnumerable<string> Keys() {
            var result = m_configFile.Keys();
            if (m_parent != null)
                result = result.Union(m_parent.Keys());
            return result;
        }
        public IEnumerator<GitConfigValue> GetEnumerator() {
            var configValues = m_parent?.ToDictionary(o => o.Key) ?? 
                new Dictionary<string, GitConfigValue>();

            foreach (var configValue in m_configFile)
                configValues[configValue.Key] = configValue;

            return configValues.Values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}