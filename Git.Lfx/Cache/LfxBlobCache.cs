using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfx {

    public sealed class LfxBlobCache : IEnumerable<LfxBlob> {
        public const string LfxDirName = @"lfx";
        public const string ObjectsDirName = @"objects";

        public static readonly string DefaultUserCacheDir = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            LfxDirName,
            ObjectsDirName
        ).ToDir();

        public static LfxBlobCache Create(string workingDir = null) {
            if (workingDir == null)
                workingDir = Environment.CurrentDirectory;
            workingDir = workingDir.ToDir();

            var gitLoader = GitConfig.Load(workingDir);

            // lfxDir -> /.git/lfx/
            var lfxDir = gitLoader.GitDirectory + LfxDirName.ToDir();

            // objectsDir -> /.git/lfx/objects/
            var objectsDir = lfxDir + ObjectsDirName.ToDir();

            var cache = Create(
                objectsDir, // L1 cache
                DefaultUserCacheDir // L2 cache
            );
            return cache;
        }

        public static LfxBlobCache Create(params string[] cacheDirs) {
            if (cacheDirs == null || cacheDirs.Length == 0)
                throw new ArgumentException(nameof(cacheDirs));

            return new LfxBlobCache(
                storeDir: cacheDirs.First(),
                parent: cacheDirs.Length == 1 ? null : 
                    Create(cacheDirs.Skip(1).ToArray())
             );
        }

        private readonly LfxBlobCache m_parentCache;
        private readonly LfxBlobStore m_store;

        public LfxBlobCache(
            string storeDir, 
            LfxBlobCache parent = null) {

            m_store = new LfxBlobStore(storeDir);
            m_parentCache = parent;
        }

        public LfxBlobCache Parent => m_parentCache;
        public LfxBlobStore Store => m_store;

        public LfxBlob Load(LfxPointer pointer) {
            LfxBlob blob;
            if (TryGet(pointer.Hash, out blob))
                return blob;

            if (pointer.Type == LfxPointerType.Archive)
                LoadArchive(pointer.Url);

            else if (pointer.Type == LfxPointerType.Curl)
                Load(pointer.Url);

            if (!TryGet(pointer.Hash, out blob))
                throw new Exception($"Expected LfxPointer hash '{pointer.Hash}' to match downloaded content.");

            return blob;
        }
        public LfxBlob Load(Uri url) {
            if (m_parentCache != null) 
                return m_store.Add(m_parentCache.Load(url));

            using (var tempFile = url.DownloadToTempFile())
                return m_store.Add(tempFile);
        }
        public IEnumerable<LfxBlob> LoadArchive(Uri url) {
            if (m_parentCache != null) 
                return m_parentCache.LoadArchive(url).Select(o => m_store.Add(o)).ToList();

            using (var tempFiles = url.DownloadAndUnZip())
                return tempFiles.Select(o => m_store.Add(o)).ToList();
        }

        public bool Promote(LfxHash hash) {
            LfxBlob blob;
            return TryGet(hash, out blob);
        }
        public bool TryGet(LfxHash hash, out LfxBlob blob) {

            // try local store
            blob = default(LfxBlob);
            if (m_store.TryGet(hash, out blob))
                return true;

            // no parent, no hope
            if (m_parentCache == null)
                return false;

            // try parent store
            LfxBlob parentBlob;
            if (!m_parentCache.TryGet(hash, out parentBlob))
                return false;

            // promote to local store
            blob = m_store.Add(parentBlob);
            return true;
        }

        public IEnumerator<LfxBlob> GetEnumerator() {
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