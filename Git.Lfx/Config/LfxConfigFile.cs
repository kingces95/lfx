using System;
using System.Collections;
using System.Collections.Generic;

namespace Git.Lfs {

    public sealed class LfsConfigFile : IEnumerable<GitConfigValue> {
        public const string FileName = ".lfsconfig";
        public const string UrlId = "lfx.url";
        public const string PatternId = "lfx.pattern";
        public const string TypeId = "lfx.type";
        public const string ArchiveHintId = "lfx.archiveHint";

        public const string CleanFilterId = "filter.lfx.clean";
        public const string SmudgeFilterId = "filter.lfx.smudge";

        public static LfsConfigFile Load(string path) {
            return new LfsConfigFile(GitConfigFile.Load(path));
        }

        private readonly GitConfigFile m_file;

        internal LfsConfigFile(GitConfigFile file) {
            m_file = file;
        }

        public GitConfigFile GitConfigFile => m_file;
        public string Path => m_file.Path;

        public string Url => m_file[UrlId];
        public string Pattern => m_file[PatternId];
        public LfsPointerType Type => m_file[TypeId].ToEnum<LfsPointerType>(ignoreCase: true);
        public string ArchiveHint => m_file[ArchiveHintId];
        public string CleanFilter => m_file[CleanFilterId];
        public string SmudgeFilter => m_file[SmudgeFilterId];

        public override string ToString() => m_file.ToString();

        public IEnumerator<GitConfigValue> GetEnumerator() {
            GitConfigValue value;

            if (m_file.TryGetValue(UrlId, out value)) yield return value;
            if (m_file.TryGetValue(PatternId, out value)) yield return value;
            if (m_file.TryGetValue(TypeId, out value)) yield return value;
            if (m_file.TryGetValue(ArchiveHintId, out value)) yield return value;
            if (m_file.TryGetValue(CleanFilterId, out value)) yield return value;
            if (m_file.TryGetValue(SmudgeFilterId, out value)) yield return value;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}