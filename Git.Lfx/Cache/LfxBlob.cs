using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfx {

    public struct LfxBlob : IEquatable<LfxBlob> {
        private const int ShortHashLength = 8;

        private readonly LfxBlobStore m_store;
        private readonly LfxHash m_hash;
        private readonly string m_file;

        internal LfxBlob(LfxBlobStore store, LfxHash hash, string path) {
            m_store = store;
            m_hash = hash;
            m_file = path;
        }

        public LfxBlobStore Store => m_store;
        public LfxHash Hash => m_hash;
        public string Path => m_file.ToString();
        public Stream OpenRead() => File.OpenRead(Path);

        public override bool Equals(object obj) => obj is LfxBlob ? Equals((LfxBlob)obj) : false;
        public bool Equals(LfxBlob other) => m_file.EqualPath(other.m_file);
        public override int GetHashCode() => Path.GetHashCode();
        public override string ToString() => $"{Hash.ToString().Substring(0, ShortHashLength)}: {Path}";
    }
}