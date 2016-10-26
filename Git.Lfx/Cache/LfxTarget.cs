using System;
using System.IO;
using Util;

namespace Git.Lfx {

    public struct LfxTarget : IEquatable<LfxTarget> {
        private const int ShortHashLength = 8;

        private readonly LfxStore m_store;
        private readonly LfxHash m_hash;
        private readonly string m_path;

        internal LfxTarget(LfxStore store, LfxHash hash, string path) {
            m_store = store;
            m_hash = hash;
            m_path = path;
        }

        public bool Exists => IsFile || IsDirectory;
        public bool IsFile => File.Exists(m_path);
        public bool IsDirectory => Directory.Exists(m_path);
        public LfxStore Store => m_store;
        public LfxHash Hash => m_hash;
        public string Path => m_path.ToString();
        public Stream OpenRead() => File.OpenRead(Path);
        public void Save(string path) {
            using (var contentStream = OpenRead()) {
                File.Delete(path);
                using (var sw = File.OpenWrite(path))
                    contentStream.CopyTo(sw);
            }
        }

        public override bool Equals(object obj) => obj is LfxTarget ? Equals((LfxTarget)obj) : false;
        public bool Equals(LfxTarget other) => m_path.EqualPath(other.m_path);
        public override int GetHashCode() => Path.GetHashCode();
        public override string ToString() => $"{Hash.ToString().Substring(0, ShortHashLength)}: {Path}";
    }
}