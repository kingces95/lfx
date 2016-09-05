using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsObject {
        private readonly LfsLoader m_loader;
        private readonly LfsHash m_hash;

        internal LfsObject(LfsLoader loader, LfsHash hash) {
            m_loader = loader;
            m_hash = hash;
        }

        public LfsLoader Loader => m_loader;
        public LfsObjectsCache Cache => Loader.RootCache;
        public LfsHash Hash => m_hash;
        public string Path => Cache.GetPath(Hash);

        public override string ToString() => Path;
    }
}