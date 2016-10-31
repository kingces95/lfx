using System.IO;
using Util;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace Git.Lfx {

    public enum LfxProgressType {
        None = 0,
        Download = 1,
        Copy = 2,
        Expand = 3,
    }

    public sealed class LfxLoader {
        private delegate string LfxDownloadDelegate(LfxHash hash, string targetPath);
        private delegate Task<string> LfxDownloadAsyncDelegate(LfxHash hash, string targetPath);
        private delegate Task<string> LfxExpandAsyncDelegate(LfxHash hash, string sourcePath, string targetPath);
        private sealed class LfxContentCache {

            private readonly AsyncSelfLoadingDirectory m_diskCache;
            private readonly AsyncSelfLoadingDirectory m_busCache;
            private readonly AsyncSelfLoadingDirectory m_lanCache;

            internal LfxContentCache(
                string diskCacheDir,
                string busCacheDir,
                string lanCacheDir = null) {

                if (lanCacheDir != null) {
                    m_lanCache = new AsyncSelfLoadingDirectory(lanCacheDir, PartitionHash);
                }

                if (busCacheDir != null) {
                    m_busCache = new AsyncSelfLoadingDirectory(busCacheDir, PartitionHash);
                    m_busCache.OnCopyProgress += progress => OnCopyProgress?.Invoke(progress);

                    // busCache delegates to lanCache, else downloads
                    m_busCache.OnTryLoadAsync += async (hash, tempPath) => {

                        // try lan cache
                        string lanCachePath = null;
                        if (m_lanCache?.TryGetPath(hash, out lanCachePath) == true)
                            return lanCachePath;

                        // download
                        return await RaiseDownloadEvent(LfxHash.Parse(hash), tempPath);
                    };
                }

                m_diskCache = new AsyncSelfLoadingDirectory(diskCacheDir, PartitionHash);
                m_diskCache.OnCopyProgress += progress => OnCopyProgress?.Invoke(progress);

                // diskCache delegates to busCache, then expands
                m_diskCache.OnTryLoadAsync += async (hash, tempPath) => {

                    // try bus cache
                    var busCachePath = await m_busCache.TryGetOrLoadPathAsync(hash);
                    if (busCachePath == null)
                        return null;

                    // expand
                    return await RaiseExpandEvent(LfxHash.Parse(hash), busCachePath, tempPath);
                };
            }

            private async Task<string> RaiseDownloadEvent(LfxHash hash, string tempPath) {
                if (OnAsyncDownload == null)
                    return null;

                // try synchronous downloading
                foreach (LfxDownloadDelegate o in 
                    OnDownload?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                    var downloadPath = o(hash, tempPath);
                    if (downloadPath == null)
                        continue;

                    return downloadPath;
                }

                // try asynchronous downloading
                foreach (LfxDownloadAsyncDelegate o in 
                    OnAsyncDownload?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                    var downloadPath = await o(hash, tempPath);
                    if (downloadPath == null)
                        continue;

                    return downloadPath;
                }

                return null;
            }
            private async Task<string> RaiseExpandEvent(LfxHash hash, string sourcePath, string tempPath) {
                if (OnAsyncExpand == null)
                    return null;

                foreach (LfxExpandAsyncDelegate expand in
                    OnAsyncExpand?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                    var expandedPath = await expand(hash, sourcePath, tempPath);
                    if (expandedPath == null)
                        continue;

                    return expandedPath;
                }

                return null;
            }

            internal event Action<long> OnCopyProgress;
            internal event LfxDownloadDelegate OnDownload;
            internal event LfxDownloadAsyncDelegate OnAsyncDownload;
            internal event LfxExpandAsyncDelegate OnAsyncExpand;

            internal bool TryGetPath(LfxHash hash, out string path) {
                return m_diskCache.TryGetPath(hash, out path);
            }
            internal async Task<string> TryGetOrLoadExpandedPathAsync(LfxHash hash) {
                return await m_diskCache.TryGetOrLoadPathAsync(hash);
            }
            internal async Task<string> TryGetOrLoadCompressedPathAsync(LfxHash hash) {
                return await m_busCache.TryGetOrLoadPathAsync(hash);
            }
            internal string GetTempDownloadPath() => m_busCache.GetTempPath();

            internal void Clean() {
                m_diskCache.Clean();
                m_busCache.Clean();
            }
        }
        private sealed class LfxUrlToHashCache {
            private static LfxHash GetUrlHash(Uri url) => LfxHash.Create(url.ToString().ToLower(), Encoding.UTF8);

            private readonly ImmutableDirectory m_busCacheDir;
            private readonly ImmutableDirectory m_lanCacheDir;

            internal LfxUrlToHashCache(
                string busCacheDir,
                string lanCacheDir) {

                m_busCacheDir = new ImmutableDirectory(busCacheDir, PartitionHash);

                if (lanCacheDir != null)
                    m_lanCacheDir = new ImmutableDirectory(lanCacheDir, PartitionHash);
            }

            internal bool TryGetValue(Uri url, out LfxHash hash) {

                hash = default(LfxHash);
                var urlHash = GetUrlHash(url);

                string hashPath;
                if (!m_busCacheDir.TryGetPath(urlHash, out hashPath)) {

                    if (m_lanCacheDir?.TryGetPath(urlHash, out hashPath) != true)
                        return false;
                }

                hash = LfxHash.Parse(File.ReadAllText(hashPath));
                    return true;
            }
            internal Task PutAsync(Uri url, LfxHash hash) {
                var urlHash = GetUrlHash(url);
                return m_busCacheDir.EchoText(urlHash, hash);
            }

            internal void Clean() {
                m_busCacheDir.Clean();
                m_lanCacheDir?.Clean();
            }
        }
        private sealed class LfxInfoCache {
            private readonly ImmutableDirectory m_diskCacheDir;
            private readonly ImmutableDirectory m_busCacheDir;
            private readonly ImmutableDirectory m_lanCacheDir;

            internal LfxInfoCache(
                string diskCacheDir,
                string busCacheDir,
                string lanCacheDir) {

                m_diskCacheDir = new ImmutableDirectory(diskCacheDir, PartitionHash);
                m_busCacheDir = new ImmutableDirectory(busCacheDir, PartitionHash);
                if (lanCacheDir != null)
                    m_lanCacheDir = new ImmutableDirectory(lanCacheDir, PartitionHash);
            }

            internal bool TryGetValue(LfxHash hash, out LfxInfo info) {
                info = default(LfxInfo);

                string infoPath;
                if (!m_diskCacheDir.TryGetPath(hash, out infoPath)) {

                    if (!m_busCacheDir.TryGetPath(hash, out infoPath)) {

                        if (m_lanCacheDir?.TryGetPath(hash, out infoPath) != true)
                            return false;
                    }
                }

                info = LfxInfo.Load(infoPath);
                return true;
            }
            internal async Task PutAsync(LfxHash hash, LfxInfo info) {
                await m_busCacheDir.EchoText(hash, info.ToString());
                await m_diskCacheDir.EchoText(hash, info.ToString());
            }

            internal void Clean() {
                m_busCacheDir.Clean();
                m_diskCacheDir.Clean();
            }
        }

        private const int L1PartitionCount = 2;
        private const int L2PartitionCount = 2;
        private const int MinimumHashLength = L1PartitionCount + L2PartitionCount;

        private static string PartitionHash(string hash) {

            if (hash.Length < MinimumHashLength)
                throw new ArgumentException(
                    $"Hash '{hash}' must be at least '{MinimumHashLength}' characters long.");

            return Path.Combine(
                hash.ToString().Substring(0, L1PartitionCount),
                hash.ToString().Substring(L1PartitionCount, L2PartitionCount),
                hash
            );
        }

        public const string PointerDirName = "pointers";
        public const string UrlToHashDirName = "urlToHash";
        public const string HashToInfoDirName = "hashToInfo";
        public const string CompressedDirName = "compressed";
        public const string ExpandedDirName = "expanded";

        private readonly ConcurrentDictionary<LfxHash, LfxPointer> m_pointers;
        private readonly LfxContentCache m_contentCache;
        private readonly LfxUrlToHashCache m_urlToHashCache;
        private readonly LfxInfoCache m_infoCache;

        public LfxLoader(
            string diskCacheDir,
            string busCacheDir,
            string lanCacheDir = null) {

            if (lanCacheDir?.EqualPath(busCacheDir) == true)
                throw new ArgumentNullException( // LanCacheDir is readonly
                    $"LanCacheDir '{lanCacheDir}' cannot equal BusCacheDir '{busCacheDir}'.");

            m_pointers = new ConcurrentDictionary<LfxHash, LfxPointer>();

            m_urlToHashCache = new LfxUrlToHashCache(
                busCacheDir: busCacheDir.PathCombine(UrlToHashDirName),
                lanCacheDir: lanCacheDir?.PathCombine(UrlToHashDirName)
            );

            m_contentCache = new LfxContentCache(
                diskCacheDir: diskCacheDir.PathCombine(ExpandedDirName),
                busCacheDir: busCacheDir.PathCombine(CompressedDirName),
                lanCacheDir: lanCacheDir?.PathCombine(CompressedDirName)
            );
            m_contentCache.OnAsyncDownload += async (expectedHash, targetPath) => {

                // lookup pointer
                var pointer = m_pointers[expectedHash];
                var url = pointer.Url;

                // download!
                var downloadHash = await DownloadAsync(url, targetPath);

                // verify hash
                if (expectedHash != null && expectedHash != downloadHash)
                    throw new Exception(
                        $"Downloaded url '{url}' content hash '{downloadHash}' " +
                        $"is different than expected hash '{expectedHash}'.");

                return targetPath;
            };
            m_contentCache.OnAsyncExpand += async (hash, compressedPath, tempDir) => {

                var pointer = m_pointers[hash];

                // cache file
                if (pointer.IsFile)
                    return compressedPath;

                // expand zip
                if (pointer.IsZip)
                    return await compressedPath.ExpandZip(tempDir, progress =>
                        RaiseProgressEvent(LfxProgressType.Expand, progress));

                // expand exe
                else if (pointer.IsExe)
                    return await compressedPath.ExpandExe(tempDir, pointer.Args, progress =>
                        RaiseProgressEvent(LfxProgressType.Expand, progress));

                throw new Exception($"Could not expand unreconginzed pointer '{pointer}'.");
            };
            m_contentCache.OnCopyProgress += progress => {
                RaiseProgressEvent(LfxProgressType.Copy, progress);
            };

            m_infoCache = new LfxInfoCache(
                diskCacheDir: diskCacheDir.PathCombine(HashToInfoDirName),
                busCacheDir: busCacheDir.PathCombine(HashToInfoDirName),
                lanCacheDir: lanCacheDir?.PathCombine(HashToInfoDirName)
            );
        }

        private async Task<LfxHash> DownloadAsync(Uri url, string targetPath) {
            
            // download!
            var byteHash = await url.DownloadAndHash(
                tempPath: targetPath, 
                onProgress: progress => 
                    RaiseProgressEvent(LfxProgressType.Download, progress)
            );

            var downloadHash = LfxHash.Create(byteHash);

            return downloadHash;
        }
        private async Task<string> GetContentAsync(LfxPointer pointer, LfxHash hash) {
            var url = pointer.Url;

            // register hash -> pointer
            m_pointers.GetOrAdd(hash, pointer);

            // load hash -> content
            var contentPath = await m_contentCache.TryGetOrLoadExpandedPathAsync(hash);

            // save url -> hash
            await m_urlToHashCache.PutAsync(url, hash);

            return contentPath;
        }

        // events
        private void RaiseProgressEvent(LfxProgressType type, long progress) {
            OnProgress?.Invoke(type, progress);
        }
        public event Action<LfxProgressType, long> OnProgress;

        // fetch
        public LfxEntry GetOrLoadEntry(LfxPointer pointer) {

            // try caches
            LfxHash hash;
            if (m_urlToHashCache.TryGetValue(pointer, out hash)) {

                LfxInfo info = default(LfxInfo);
                if (m_infoCache.TryGetValue(hash, out info)) {
                    var contentPath = GetContentAsync(pointer, hash).Await();
                    return LfxEntry.Create(info, contentPath);
                }
            }

            // download to staging path
            var url = pointer.Url;
            var downloadPath = m_contentCache.GetTempDownloadPath();
            hash = DownloadAsync(url, downloadPath).Await();
                
            // populate cache with downloaded file
            LfxDownloadDelegate downloadDelegate = (expectedHash, _) => {
                return expectedHash == hash ? downloadPath : null;
            };
            m_contentCache.OnDownload += downloadDelegate;
            try {

                // create info!
                var contentPath = GetContentAsync(pointer, hash).Await();
                var compressedPath = m_contentCache.TryGetOrLoadCompressedPathAsync(hash).Await();
                var info = LfxInfo.Create(pointer, contentPath, compressedPath);

                // cache info
                GetOrLoadContentAsync(info).Await();

                return LfxEntry.Create(info, contentPath);
            } 
            finally {
                m_contentCache.OnDownload -= downloadDelegate;
            }
        }
        public async Task<string> GetOrLoadContentAsync(LfxInfo info) {
            var contentPath = await GetContentAsync(info.Pointer, info.Hash);

            // cache info
            await m_infoCache.PutAsync(info.Hash, info);

            return contentPath;
        }

        // housekeeping
        public void Clean() {
            m_contentCache.Clean();
            m_urlToHashCache.Clean();
            m_infoCache.Clean();
        }
    }
}