using System;
using System.Collections;
using System.Collections.Generic;

namespace Git.Lfx {

    public sealed class LfxConfigFile : IEnumerable<GitConfigValue> {
        public const string FileName = ".lfxconfig";
        public const string UrlId = "lfx.url";
        public const string PatternId = "lfx.pattern";
        public const string TypeId = "lfx.type";
        public const string HintId = "lfx.hint";
        public const string ArgsId = "lfx.args";

        public const string CleanFilterId = "filter.lfx.clean";
        public const string SmudgeFilterId = "filter.lfx.smudge";

        public static LfxConfigFile Load(string path) {
            return new LfxConfigFile(GitConfigFile.Load(path));
        }

        private readonly GitConfigFile m_file;

        internal LfxConfigFile(GitConfigFile file) {
            m_file = file;
        }

        public GitConfigFile GitConfigFile => m_file;
        public string Path => m_file.Path;

        public string Url => m_file[UrlId];
        public string Pattern => m_file[PatternId];
        public LfxPointerType Type => m_file[TypeId].ToEnum<LfxPointerType>(ignoreCase: true);
        public string Hint => m_file[HintId];
        public string Args => m_file[ArgsId];
        public string CleanFilter => m_file[CleanFilterId];
        public string SmudgeFilter => m_file[SmudgeFilterId];

        public override string ToString() => m_file.ToString();

        public IEnumerator<GitConfigValue> GetEnumerator() {
            GitConfigValue value;

            if (m_file.TryGetValue(UrlId, out value)) yield return value;
            if (m_file.TryGetValue(PatternId, out value)) yield return value;
            if (m_file.TryGetValue(TypeId, out value)) yield return value;
            if (m_file.TryGetValue(HintId, out value)) yield return value;
            if (m_file.TryGetValue(ArgsId, out value)) yield return value;
            if (m_file.TryGetValue(CleanFilterId, out value)) yield return value;
            if (m_file.TryGetValue(SmudgeFilterId, out value)) yield return value;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}