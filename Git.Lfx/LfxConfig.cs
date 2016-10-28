using System.IO;
using Util;

namespace Git.Lfx {

    public sealed class LfxConfig {

        public static LfxConfig Load(string path = null) {
            if (path != null) {
                path += LfxConfigFile.FileName;
                if (File.Exists(path))
                    return Load(GitConfig.Load(path));

                path = path.GetDir();
            }

            var gitConfig = GitConfig.Load(path, LfxConfigFile.FileName);
            if (gitConfig == null)
                return null;

            return Load(gitConfig);
        }
        public static LfxConfig Load(GitConfig gitConfig) {
            if (gitConfig == null)
                return null;
            return new LfxConfig(gitConfig);
        }

        private readonly LfxConfig m_parent;
        private readonly GitConfig m_gitConfig;
        private readonly LfxConfigFile m_configFile;

        private LfxConfig(GitConfig gitConfig) {
            m_gitConfig = gitConfig;
            m_configFile = new LfxConfigFile(m_gitConfig.ConfigFile);
            if (m_gitConfig.Parent != null)
                m_parent = new LfxConfig(m_gitConfig.Parent);
        }

        private GitConfigValue? TryGetValue(string id) {
            GitConfigValue value;
            if (!m_gitConfig.TryGetValue(id, out value))
                return null;
            return value;
        }

        public GitConfig GitConfig => m_gitConfig;
        public LfxConfigFile ConfigFile => m_configFile;
        public LfxConfig Parent => m_parent;
        public LfxIdType Type {
            get { return m_gitConfig[LfxConfigFile.TypeId].ToEnum<LfxIdType>(ignoreCase: true); }
        }
        public string Url => m_gitConfig[LfxConfigFile.UrlId];
        public bool HasPattern => m_gitConfig.Contains(LfxConfigFile.PatternId);
        public GitConfigValue Pattern => m_gitConfig.GetValue(LfxConfigFile.PatternId);
        public bool HasHint => m_gitConfig.Contains(LfxConfigFile.HintId);
        public GitConfigValue Hint => m_gitConfig.GetValue(LfxConfigFile.HintId);
        public bool HasArgs => m_gitConfig.Contains(LfxConfigFile.ArgsId);
        public GitConfigValue Args => m_gitConfig.GetValue(LfxConfigFile.ArgsId);

        public override string ToString() => m_gitConfig.ToString();
    }
}