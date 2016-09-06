using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsBlobCache {
        private readonly LfsLoader m_loader;
        private readonly LfsBlobCache m_parentCache;
        private readonly LfsBlobStore m_store;

        public LfsBlobCache(
            LfsLoader loader,
            string objectsDir, 
            LfsBlobCache parent = null) {

            m_store = new LfsBlobStore(objectsDir);
            m_loader = loader;
            m_parentCache = parent;
        }

        public LfsLoader Loader => m_loader;
        public LfsBlobCache Parent => m_parentCache;
        public LfsBlobStore Store => m_store;
        public bool TryGet(LfsHash hash, out LfsBlob blob) {

            // try local store
            blob = default(LfsBlob);
            if (m_store.TryGet(hash, out blob))
                return true;

            // no parent, no hope
            if (m_parentCache == null)
                return false;

            // try parent store
            if (!m_parentCache.TryGet(hash, out blob))
                return false;

            // promote to local store
            m_store.Add(blob);
            return true;
        }
        public LfsBlob Load(LfsPointer pointer) {
            LfsBlob blob;
            if (TryGet(pointer.Hash, out blob))
                return blob;

            if (pointer.Type == LfsPointerType.Archive)
                LoadArchive(pointer.Url);

            else if (pointer.Type == LfsPointerType.Curl)
                Load(pointer.Url);

            TryGet(pointer.Hash, out blob);
            if (blob.Hash != pointer.Hash)
                throw new Exception($"Expected resolved LfsPointer hash '{pointer.Hash}' to match its hash: '{blob.Hash}'");

            return blob;
        }
        public LfsBlob Load(Uri url) {
            if (m_parentCache != null) 
                return m_store.Add(m_parentCache.Load(url));

            using (var tempFile = url.DownloadToTempFile())
                return m_store.Add(tempFile);
        }
        public IEnumerable<LfsBlob> LoadArchive(Uri url) {
            if (m_parentCache != null) 
                return m_parentCache.LoadArchive(url).Select(o => m_store.Add(o)).ToList();

            using (var tempFiles = url.DownloadAndUnZip())
                return tempFiles.Select(o => m_store.Add(o)).ToList();
        }
    }
}