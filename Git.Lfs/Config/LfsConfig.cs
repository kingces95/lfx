using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Git.Lfs {

    public sealed class LfsConfig {

        public static LfsConfig Load(string path = null) {
            if (path != null) {
                path += LfsConfigFile.FileName;
                if (File.Exists(path))
                    return Load(GitConfig.Load(path));

                path = path.GetDir();
            }

            var gitConfig = GitConfig.Load(path, LfsConfigFile.FileName);
            if (gitConfig == null)
                return null;

            return Load(gitConfig);
        }
        public static LfsConfig Load(GitConfig gitConfig) {
            if (gitConfig == null)
                return null;
            return new LfsConfig(gitConfig);
        }

        private readonly LfsConfig m_parent;
        private readonly GitConfig m_gitConfig;
        private readonly LfsConfigFile m_configFile;

        private LfsConfig(GitConfig gitConfig) {
            m_gitConfig = gitConfig;
            m_configFile = new LfsConfigFile(m_gitConfig.ConfigFile);
            if (m_gitConfig.Parent != null)
                m_parent = new LfsConfig(m_gitConfig.Parent);
        }

        private GitConfigValue? TryGetValue(string id) {
            GitConfigValue value;
            if (!m_gitConfig.TryGetValue(id, out value))
                return null;
            return value;
        }

        public GitConfig GitConfig => m_gitConfig;
        public LfsConfigFile ConfigFile => m_configFile;
        public LfsConfig Parent => m_parent;
        public LfsPointerType Type {
            get { return m_gitConfig[LfsConfigFile.TypeId].ToEnum<LfsPointerType>(ignoreCase: true); }
        }
        public string Url => m_gitConfig[LfsConfigFile.UrlId];
        public bool HasPattern => m_gitConfig.Contains(LfsConfigFile.PatternId);
        public GitConfigValue Pattern => m_gitConfig.GetValue(LfsConfigFile.PatternId);
        public bool HasArchiveHint => m_gitConfig.Contains(LfsConfigFile.ArchiveHintId);
        public GitConfigValue ArchiveHint => m_gitConfig.GetValue(LfsConfigFile.ArchiveHintId);

        public override string ToString() => m_gitConfig.ToString();
    }
}