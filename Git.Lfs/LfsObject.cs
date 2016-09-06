using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsObject {
        private readonly LfsLoader m_loader;
        private readonly LfsBlob m_blob;

        internal LfsObject(LfsLoader loader, LfsBlob blob) {
            m_loader = loader;
            m_blob = blob;
        }

        public LfsLoader Loader => m_loader;
        public LfsBlobCache Cache => Loader.Cache;
        public LfsBlobStore Store => Cache.Store;
        public LfsBlob Blob => m_blob;
        public LfsHash Hash => m_blob.Hash;
        public string Path => m_blob.Path;

        public override string ToString() => Path;
    }
}