using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsBlobCache : IEnumerable<LfsBlob> {
        public static readonly string DefaultUserCacheDir =
            Environment.GetEnvironmentVariable("APPDATA") +
                Path.DirectorySeparatorChar + "lfsEx";

        public static LfsBlobCache Create(params string[] cacheDirs) {
            if (cacheDirs == null || cacheDirs.Length == 0)
                throw new ArgumentException(nameof(cacheDirs));

            return new LfsBlobCache(
                storeDir: cacheDirs.First(),
                parent: cacheDirs.Length == 1 ? null : 
                    Create(cacheDirs.Skip(1).ToArray())
             );
        }

        private readonly LfsBlobCache m_parentCache;
        private readonly LfsBlobStore m_store;

        public LfsBlobCache(
            string storeDir, 
            LfsBlobCache parent = null) {

            m_store = new LfsBlobStore(storeDir);
            m_parentCache = parent;
        }

        public LfsBlobCache Parent => m_parentCache;
        public LfsBlobStore Store => m_store;

        public LfsBlob Load(LfsPointer pointer) {
            LfsBlob blob;
            if (TryGet(pointer.Hash, out blob))
                return blob;

            if (pointer.Type == LfsPointerType.Archive)
                LoadArchive(pointer.Url);

            else if (pointer.Type == LfsPointerType.Curl)
                Load(pointer.Url);

            if (!TryGet(pointer.Hash, out blob))
                throw new Exception($"Expected LfsPointer hash '{pointer.Hash}' to match downloaded content.");

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

        public bool Promote(LfsHash hash) {
            LfsBlob blob;
            return TryGet(hash, out blob);
        }
        public bool TryGet(LfsHash hash, out LfsBlob blob) {

            // try local store
            blob = default(LfsBlob);
            if (m_store.TryGet(hash, out blob))
                return true;

            // no parent, no hope
            if (m_parentCache == null)
                return false;

            // try parent store
            LfsBlob parentBlob;
            if (!m_parentCache.TryGet(hash, out parentBlob))
                return false;

            // promote to local store
            blob = m_store.Add(parentBlob);
            return true;
        }

        public IEnumerator<LfsBlob> GetEnumerator() {
            // promote parent blobs to child
            if (m_parentCache != null) {
                foreach (var blob in m_parentCache)
                    Promote(blob.Hash);
            }

            return m_store.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}