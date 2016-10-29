using System.IO;
using Util;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Collections.Generic;
using System.Collections;

namespace Git.Lfx {

    public enum LfxProgressType {
        None = 0,
        Download = 1,
        Copy = 2,
        Expand = 3,
    }

    public abstract class LfxCache : 
        ImmutableAsyncDictionary<LfxHash, LfxId, string>,
        IEnumerable<KeyValuePair<LfxPointer, string>>,
        IDisposable {

        public const string PointerDirName = "pointers";
        public const string UrlToHashDirName = "urlToHash";
        public const string HashToPointerDirName = "hashToPointer";
        public const string CompressedDirName = "compressed";
        public const string ExpandedDirName = "expanded";

        public static LfxCache CreateCache(
            string diskCacheDir,
            string busCacheDir,
            string lanCacheDir) {

            if (busCacheDir == null)
                throw new ArgumentNullException(busCacheDir);

            LfxCache lanCache = null;

            // readonly (aka "lan") cache
            if (lanCacheDir != null)

                if (lanCacheDir.EqualPath(busCacheDir))
                    throw new ArgumentNullException(
                        $"LanCacheDir '{lanCacheDir}' cannot equal BusCacheDir '{busCacheDir}'.");

                if (lanCacheDir.EqualPath(diskCacheDir))
                    throw new ArgumentNullException(
                        $"LanCacheDir '{lanCacheDir}' cannot equal BusCacheDir '{diskCacheDir}'.");

                lanCache = new LfxReadOnlyCache(
                    Path.Combine(lanCacheDir, CompressedDirName),
                    Path.Combine(lanCacheDir, UrlToHashDirName),
                    Path.Combine(lanCacheDir, HashToPointerDirName)
                );

            // download (aka "bus") cache
            var busCache = new LfxDownloadCache(
                Path.Combine(busCacheDir, CompressedDirName),
                Path.Combine(busCacheDir, UrlToHashDirName),
                Path.Combine(busCacheDir, HashToPointerDirName),
                lanCache
            );

            // expanded (aka "disk") cache
            var cache = new LfxExpandedCache(
                Path.Combine(diskCacheDir, ExpandedDirName),
                Path.Combine(diskCacheDir, HashToPointerDirName),
                busCache
            );

            return cache;
        }

        private readonly LfxCache m_parent;
        private readonly ImmutablePathDictionary m_hashToContent;
        private readonly ImmutablePathDictionary m_urlToHash;
        private readonly ImmutablePathDictionary m_hashToPointer;

        protected LfxCache(
            string dir,
            string urlToHashDir,
            string hashToPointer,
            LfxCache parent) {

            m_parent = parent;

            // stores
            m_hashToContent = new ImmutablePathDictionary(dir);
            if (urlToHashDir != null)
                m_urlToHash = new ImmutablePathDictionary(urlToHashDir);
            m_hashToPointer = new ImmutablePathDictionary(hashToPointer);

            // report copy progress
            m_hashToContent.OnCopyProgress += 
                progress => ReportProgress(LfxProgressType.Copy, progress);

            // bubble parent progress
            if (parent != null)
                parent.OnProgress += (type, progress) => ReportProgress(type, progress);
        }

        private LfxPointer CreatePointer(LfxId id) {

            // get compressed path
            var compressedPath = m_parent.GetOrLoadValueAsync(id).Await();

            // populate expanded cache
            var cachePath = GetOrLoadValueAsync(id).Await();

            // create poitner
            var pointer = LfxPointer.Create(id, cachePath, compressedPath);

            return pointer;
        }

        // stores
        protected ImmutablePathDictionary HashToContent => m_hashToContent;
        protected ImmutablePathDictionary UrlToHash => m_urlToHash;
        protected ImmutablePathDictionary HashToPointer => m_hashToPointer;

        // helpers
        protected LfxHash GetUrlHash(Uri url) => url.ToString().ToLower().GetHash(Encoding.UTF8);
        protected string GetTempPath() => Path.Combine(m_hashToContent.TempDir, Path.GetRandomFileName());
        protected void ReportProgress(LfxProgressType type, long progress) => OnProgress?.Invoke(type, progress);
        protected bool TryGetHash(Uri url, out LfxHash hash) {
            var urlHash = GetUrlHash(url);

            // try local store, then parent
            string hashPath = null;
            if (m_urlToHash?.TryGetValue(urlHash, out hashPath) != true)
                return m_parent?.TryGetHash(url, out hash) ?? false;

            hash = LfxHash.Parse(File.ReadAllText(hashPath));
            return true;
        }

        // virtuals
        protected virtual LfxPointer FetchUrl(Uri url, Func<LfxHash, LfxPointer> getPointer) {

            // no override so no choice but to ask parent
            if (m_parent == null)
                throw new InvalidOperationException(
                    $"LfxCache '{this}' failed to resolve url '{url}'.");

            // try parent
            var pointer = m_parent.FetchUrl(url, getPointer);

            // cache pointer
            m_hashToPointer.PutText(pointer.Value, pointer.Hash).Await();

            return pointer;
        }
        protected virtual bool IsReadOnly => false;

        // overrides
        protected sealed override LfxHash GetKey(LfxId id) => id.Hash;
        protected sealed override bool TryLoadValue(LfxHash hash, out string path) {
            return m_hashToContent.TryGetValue(hash, out path);
        }
        protected override async Task<string> LoadValueAsync(LfxId id) {
            return await m_parent?.GetOrLoadValueAsync(id);
        }

        // events
        public event Action<LfxProgressType, long> OnProgress;

        // fetch
        public LfxPointer FetchFile(Uri url) {
            return FetchUrl(url, hash => CreatePointer(LfxId.CreateFile(url, hash)));
        }
        public LfxPointer FetchZip(Uri url) {
            return FetchUrl(url, hash => CreatePointer(LfxId.CreateZip(url, hash)));
        }
        public LfxPointer FetchExe(Uri url, string args) {
            return FetchUrl(url, hash => CreatePointer(LfxId.CreateExe(url, args, hash)));
        }

        // reflection
        public IEnumerator<KeyValuePair<LfxPointer, string>> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // housekeeping
        public void Clean() {
            if (IsReadOnly)
                return;

            m_hashToContent.Clean();
            m_urlToHash?.Clean();
            m_hashToPointer.Clean();
            m_parent?.Clean();
        }
        public void Dispose() {
            m_hashToContent?.Dispose();
        }

        // object
        public override string ToString() => m_hashToContent.ToString();
    }
    internal sealed class LfxExpandedCache : LfxCache {

        public LfxExpandedCache(
            string dir,
            string hashToPointer,
            LfxCache parent) 
            : base(dir, null, hashToPointer, parent) {

            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
        }

        private void ReportExpansionProgress(long progress) => ReportProgress(LfxProgressType.Expand, progress);

        protected override async Task<string> LoadValueAsync(LfxId id) {

            // try parent
            var compressedPath = await base.LoadValueAsync(id);
            if (compressedPath == null)
                throw new ArgumentException($"Url '{id.Url}' failed to resolve.");

            // cache path
            var hash = Path.GetFileName(compressedPath);

            // cache file
            if (id.IsFile)
                return await HashToContent.Put(compressedPath, hash, preferHardLink: true);

            // expand archive
            var tempDir = GetTempPath();
            if (id.IsZip)
                await compressedPath.ExpandZip(tempDir, ReportExpansionProgress);

            else if (id.IsExe)
                await compressedPath.ExpandExe(tempDir, id.Args, ReportExpansionProgress);

            // cache directory
            return await HashToContent.Take(tempDir, hash);
        }
    }
    internal sealed class LfxDownloadCache : LfxCache {

        public LfxDownloadCache(
            string dir,
            string urlToHashDir,
            string hashToPointer,
            LfxCache parent) 
            : base(dir, urlToHashDir, hashToPointer, parent) {
        }

        private void ReportDownloadProgress(long progress) => ReportProgress(LfxProgressType.Download, progress);
        private async Task<string> Download(Uri url, LfxHash? expectedHash = null) {

            var tempPath = GetTempPath();

            // download!
            var byteHash = await url.DownloadAndHash(tempPath, onProgress: ReportDownloadProgress);

            // verify hash
            var downloadHash = LfxHash.Create(byteHash);
            if (expectedHash != null && expectedHash != downloadHash)
                throw new Exception($"Downloaded url '{url}' hash '{downloadHash}' is different than expected hash '{expectedHash}'.");

            // stash hash -> content
            var compressedPath = await HashToContent.Take(tempPath, downloadHash);

            // stash url -> hash
            await UrlToHash.PutText(
                downloadHash,
                GetUrlHash(url)
            );

            return compressedPath;
        }

        protected override LfxPointer FetchUrl(Uri url, Func<LfxHash, LfxPointer> getPointer) {

            // try get hash
            LfxHash hash;
            if (!TryGetHash(url, out hash)) {

                // download!
                var downloadPath = Download(url).Await();

                // get hash
                TryGetHash(url, out hash);
            }

            // create poitner
            var pointer = getPointer(hash);

            // cache pointer
            HashToPointer.PutText(pointer.Value, pointer.Hash).Await();

            return pointer;
        }
        protected override async Task<string> LoadValueAsync(LfxId id) {

            // try parent
            var compressedPath = await base.LoadValueAsync(id);
            if (compressedPath != null)
                return await HashToContent.Put(compressedPath, id.Hash);

            return await Download(id.Url, id.Hash);
        }
    }
    internal sealed class LfxReadOnlyCache : LfxCache {

        public LfxReadOnlyCache(
            string dir,
            string urlToHashDir,
            string hashToPointer)
            : base(dir, urlToHashDir, hashToPointer, null) { }

        protected override Task<string> LoadValueAsync(LfxId id) {
            return Task.FromResult(default(string));
        }
        protected override bool IsReadOnly => true;
    }
}