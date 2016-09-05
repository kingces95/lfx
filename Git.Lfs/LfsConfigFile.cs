using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;
using System.Collections;

namespace Git.Lfs {

    public sealed class LfsConfigFile : IEnumerable<KeyValuePair<string, string>> {
        public const string FileName = ".lfsconfig";
        public const string UrlId = "lfsEx.url";
        public const string RegexId = "lfsEx.regex";
        public const string TypeId = "lfsEx.type";
        public const string HintId = "lfsEx.hint";

        private readonly LfsLoader m_loader;
        private readonly string m_path;
        private readonly string m_directory;
        private readonly GitConfigFile m_config;

        internal LfsConfigFile(LfsLoader loader, string path) {
            if (string.Compare(IOPath.GetFileName(path), FileName, ignoreCase: true) != 0)
                throw new Exception($"Expected LsfConfigFile path '{path}' to have name '.lfsconfig'.");

            m_path = path;
            m_config = new GitConfigFile(path);
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

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => m_config.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class GitConfigFile : IEnumerable<KeyValuePair<string, string>> {
        private const string ConfigRegexName = "name";
        private const string ConfigRegexValue = "value";
        private static readonly string ConfigRegex =
            $"(?<{ConfigRegexName}>[^=]*)=(?<{ConfigRegexValue}>.*)";

        private Dictionary<string, string> m_config;

        public GitConfigFile(string configFile) {
            var sr = Cmd.Execute("git.exe", $"config -l -f {configFile}");
            m_config = new Dictionary<string, string>(
                StringComparer.InvariantCultureIgnoreCase);

            while (true) {
                var line = sr.ReadLine();
                if (line == null)
                    break;

                var match = Regex.Match(line, ConfigRegex);
                var name = match.Groups[ConfigRegexName].Value;
                var value = match.Groups[ConfigRegexValue].Value;
                m_config[name] = value;
            }
        }

        public string this[string key] {
            get {
                string value;
                m_config.TryGetValue(key, out value);
                return value;
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => m_config.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}