using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;

namespace Git.Lfs {

    public sealed class LfsConfig {
        public const string FileName = ".lfsconfig";
        public const string UrlId = "lfs.url";
        public const string RegexId = "lfs.regex";
        public const string TypeId = "lfs.type";
        public const string HintId = "lfs.hint";

        public static LfsConfig Find(string file) {
            var configPath = FindFileAbove(file, FileName).FirstOrDefault();
            if (configPath == null)
                return null;
            return new LfsConfig(configPath);
        }
        private static IEnumerable<string> FindFileAbove(string file, string targetFileName) {

            return FindFileAbove(
                new DirectoryInfo(IOPath.GetDirectoryName(file)),
                targetFileName
            );
        }
        private static IEnumerable<string> FindFileAbove(DirectoryInfo dir, string targetFileName) {
            if (dir == null)
                yield break;

            while (dir != null) {
                var target = dir.GetFiles(targetFileName).ToArray();
                if (target.Length == 1)
                    yield return target[0].FullName;
                dir = dir.Parent;
            }
        }

        private readonly string m_path;
        private readonly string m_directory;
        private readonly GitConfig m_config;

        public LfsConfig(string path) {
            m_path = path;
            m_config = new GitConfig(path);
            m_directory = IOPath.GetDirectoryName(path) + IOPath.DirectorySeparatorChar;

            if (Type == LfsPointerType.Archive || Type == LfsPointerType.Curl) {
                if (Url == null)
                    throw new Exception($"Expected '{UrlId}' in '{path}'.");

                if (Regex == null)
                    throw new Exception($"Expected '{RegexId}' in '{path}'.");
            }
        }

        public string Path => m_path;
        public string Directory => m_directory;
        public string Url => m_config[UrlId];
        public string Regex => m_config[RegexId];
        public LfsPointerType Type => m_config[TypeId] == null ? LfsPointerType.Simple : (LfsPointerType)
            Enum.Parse(typeof(LfsPointerType), m_config[TypeId], ignoreCase: true);
        public string Hint => m_config[HintId];
    }

    public sealed class GitConfig {
        private const string ConfigRegexName = "name";
        private const string ConfigRegexValue = "value";
        private static readonly string ConfigRegex =
            $"(?<{ConfigRegexName}>[^=]*)=(?<{ConfigRegexValue}>.*)";

        private Dictionary<string, string> m_config;

        public GitConfig(string configFile) {
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
    }
}