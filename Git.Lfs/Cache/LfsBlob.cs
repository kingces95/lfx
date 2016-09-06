using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public struct LfsBlob : IEquatable<LfsBlob> {
        private readonly LfsBlobStore m_store;
        private readonly LfsHash m_hash;
        private readonly string m_file;

        internal LfsBlob(LfsBlobStore store, LfsHash hash, string path) {
            m_store = store;
            m_hash = hash;
            m_file = path;
        }

        public LfsBlobStore Store => m_store;
        public LfsHash Hash => m_hash;
        public string Path => m_file.ToString();

        public override bool Equals(object obj) => obj is LfsBlob ? Equals((LfsBlob)obj) : false;
        public bool Equals(LfsBlob other) => m_file.EqualPath(other.m_file);
        public override int GetHashCode() => Path.GetHashCode();
        public override string ToString() => $"{Hash}: {Path}";
    }
}