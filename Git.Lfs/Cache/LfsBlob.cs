using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public struct LfsBlob {
        private readonly LfsBlobStore m_store;
        private readonly LfsHash m_hash;
        private readonly string m_path;

        internal LfsBlob(LfsBlobStore store, LfsHash hash, string path) {
            m_store = store;
            m_hash = hash;
            m_path = path;
        }

        public LfsBlobStore Store => m_store;
        public LfsHash Hash => m_hash;
        public string Path => m_path;

        public override string ToString() => $"{Hash}: {Path}";
    }
}