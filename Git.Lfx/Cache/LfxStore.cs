using System.IO;
using Util;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Security.Cryptography;
using System.Net;

namespace Git.Lfx {

    public enum LfxProgressType {
        None = 0,
        Download = 1,
        Copy = 2,
        Expand = 3,
    }

    public abstract class LfxCache : 
        ImmutableAsyncDictionary<LfxHash, LfxId, string>,
        IDisposable {

        public const string PointerDirName = "pointers";
        public const string UrlToHashDirName = "urlToHash";
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
                    Path.Combine(lanCacheDir, UrlToHashDirName)
                );

            // download (aka "bus") cache
            var busCache = new LfxDownloadCache(
                Path.Combine(busCacheDir, CompressedDirName),
                Path.Combine(busCacheDir, UrlToHashDirName),
                lanCache
            );

            // expanded (aka "disk") cache
            var cache = new LfxExpandedCache(
                Path.Combine(diskCacheDir, ExpandedDirName),
                busCache
            );

            return cache;
        }

        private readonly LfxCache m_parent;
        private readonly ImmutablePathDictionary m_hashToContent;
        private readonly ImmutablePathDictionary m_urlToHash;

        protected LfxCache(
            string dir,
            string urlToHashDir,
            LfxCache parent) {

            m_parent = parent;

            if (urlToHashDir != null)
                m_urlToHash = new ImmutablePathDictionary(urlToHashDir);

            m_hashToContent = new ImmutablePathDictionary(dir);

            // report copy progress
            m_hashToContent.OnCopyProgress += 
                progress => ReportProgress(LfxProgressType.Copy, progress);

            // bubble parent progress
            if (parent != null)
                parent.OnProgress += (type, progress) => ReportProgress(type, progress);
        }

        protected ImmutablePathDictionary HashToContent => m_hashToContent;
        protected ImmutablePathDictionary UrlToHash => m_urlToHash;

        protected LfxCache Parent => m_parent;
        protected LfxHash GetUrlHash(Uri url) => url.ToString().ToLower().GetHash(Encoding.UTF8);
        protected void ReportProgress(LfxProgressType type, long progress) {
            OnProgress?.Invoke(type, progress);
        }
        protected string GetTempPath() => Path.Combine(m_hashToContent.TempDir, Path.GetRandomFileName());
        protected bool TryGetHash(Uri url, out LfxHash hash) {
            var urlHash = GetUrlHash(url);

            // try local store, then parent
            string hashPath = null;
            if (m_urlToHash?.TryGetValue(urlHash, out hashPath) != true)
                return m_parent?.TryGetHash(url, out hash) ?? false;

            hash = LfxHash.Parse(File.ReadAllText(hashPath));
            return true;
        }
        protected virtual LfxPointer FetchUrl(Uri url, Func<LfxHash, LfxId> getId) {
            throw new NotSupportedException();
        }

        protected sealed override LfxHash GetKey(LfxId id) => id.Hash;
        protected override bool TryLoadValue(LfxHash hash, out string path) {
            return m_hashToContent.TryGetValue(hash, out path);
        }
        protected abstract override Task<string> LoadValueAsync(LfxId id);

        public event Action<LfxProgressType, long> OnProgress;

        public LfxPointer FetchFile(Uri url) {
            return FetchUrl(url, hash => LfxId.CreateFile(url, hash));
        }
        public LfxPointer FetchZip(Uri url) {
            return FetchUrl(url, hash => LfxId.CreateZip(url, hash));
        }
        public LfxPointer FetchExe(Uri url, string args) {
            return FetchUrl(url, hash => LfxId.CreateExe(url, args, hash));
        }

        public void Dispose() {
            m_hashToContent?.Dispose();
        }
        public override string ToString() => m_hashToContent.ToString();
    }
    internal sealed class LfxExpandedCache : LfxCache {

        public LfxExpandedCache(
            string dir,
            LfxCache parent) 
            : base(dir, null, parent) {

            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
        }

        private void ReportExpansionProgress(long progress) {
            ReportProgress(LfxProgressType.Expand, progress);
        }

        protected override LfxPointer FetchUrl(Uri url, Func<LfxHash, LfxId> getId) {

            // get hash
            LfxHash hash;
            if (!TryGetHash(url, out hash)) {

                // download url
                var downloadPath = ((LfxDownloadCache)Parent).Download(url).Await();

                // get hash
                TryGetHash(url, out hash);
            }

            var id = getId(hash);
            var compressedPath = Parent.GetOrLoadValueAsync(id).Await();

            // populate cache
            var cachePath = GetOrLoadValueAsync(id).Await();

            // create poitner
            return LfxPointer.Create(id, cachePath, compressedPath);
        }
        protected override async Task<string> LoadValueAsync(LfxId id) {

            // try parent
            var compressedPath = await Parent.GetOrLoadValueAsync(id);
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
            LfxCache parent) 
            : base(dir, urlToHashDir, parent) {
        }

        private void ReportDownloadProgress(long progress) {
            ReportProgress(LfxProgressType.Download, progress);
        }

        internal async Task<string> Download(Uri url, LfxHash? hash = null) {

            var tempPath = GetTempPath();

            // download!
            var downloadHash = (await url.DownloadAndHash(tempPath, onProgress: ReportDownloadProgress)).GetHash();

            // verify hash
            if (hash != null && hash != downloadHash)
                throw new Exception($"Downloaded url '{url}' hash '{downloadHash}' is different than expected hash '{hash}'.");

            // stash hash -> content
            var compressedPath = await HashToContent.Take(tempPath, downloadHash);

            // stash url -> hash
            await UrlToHash.PutText(
                downloadHash,
                GetUrlHash(url)
            );

            return compressedPath;
        }

        protected override async Task<string> LoadValueAsync(LfxId id) {

            // try parent
            var compressedPath = await Parent?.GetOrLoadValueAsync(id);
            if (compressedPath != null)
                return await HashToContent.Put(compressedPath, id.Hash);

            return await Download(id.Url, id.Hash);
        }
    }
    internal sealed class LfxReadOnlyCache : LfxCache {

        public LfxReadOnlyCache(
            string dir,
            string urlToHashDir) 
            : base(dir, urlToHashDir, null) { }

        protected override Task<string> LoadValueAsync(LfxId id) {
            return Task.FromResult(default(string));
        }
    }
}