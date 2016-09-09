using System;
using System.Collections.Generic;
using IOPath = System.IO.Path;
using System.Collections;

namespace Git.Lfs {

    public sealed class LfsConfigFile : IEnumerable<GitConfigValue> {
        public const string FileName = ".lfsconfig";
        public const string UrlId = "lfx.url";
        public const string RegexId = "lfx.regex";
        public const string TypeId = "lfx.type";
        public const string HintId = "lfx.hint";

        private readonly LfsLoader m_loader;
        private readonly string m_path;
        private readonly string m_directory;
        private readonly GitConfigFile m_config;

        internal LfsConfigFile(LfsLoader loader, string path) {
            if (string.Compare(IOPath.GetFileName(path), FileName, ignoreCase: true) != 0)
                throw new Exception($"Expected LsfConfigFile path '{path}' to have name '.lfsconfig'.");

            m_loader = loader;
            m_path = path;
            m_config = GitConfigFile.Create(path);
            m_directory = IOPath.GetDirectoryName(path) + IOPath.DirectorySeparatorChar;

            if (Type == LfsPointerType.Archive || Type == LfsPointerType.Curl) {
                if (Url == null)
                    throw new Exception($"Expected '{UrlId}' in '{path}'.");

                if (Regex == null)
                    throw new Exception($"Expected '{RegexId}' in '{path}'.");
            }
        }

        public LfsLoader Loader => m_loader;
        public string Path => m_path;
        public string Directory => m_directory;
        public string Url => m_config[UrlId];
        public string Regex => m_config[RegexId];
        public LfsPointerType Type => m_config[TypeId] == null ? LfsPointerType.Simple : (LfsPointerType)
            Enum.Parse(typeof(LfsPointerType), m_config[TypeId], ignoreCase: true);
        public string Hint => m_config[HintId];

        public IEnumerator<GitConfigValue> GetEnumerator() => m_config.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => m_config.ToString();
    }
}