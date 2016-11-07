using System.IO;
using Util;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Net;

namespace Lfx {

    [Flags]
    public enum LfxProgressType {
        None = 0,
        Download = 1 << 0,
        Copy = 1 << 2,
        Expand = 1 << 3,
    }

    [Flags]
    public enum LfxLoadAction {
        None,

        ExpandMask = 0x3, // 0b11
        Copy = 1 << 0,
        Expand = 2 << 0,

        DownloadMask = 0x1C, // 0b11100
        Bus = 1 << 2,
        Lan = 2 << 2,
        Wan = 3 << 2,
    }

    public struct LfxProgress {
        public static LfxProgress Download(Uri sourceUrl, string targetPath, long? bytes) {
            return new LfxProgress(LfxProgressType.Download, sourceUrl.ToString(), targetPath, bytes);
        }
        public static LfxProgress Copy(string sourcePath, string targetPath, long? bytes) {
            return new LfxProgress(LfxProgressType.Copy, sourcePath, targetPath, bytes);
        }
        public static LfxProgress Expand(string sourcePath, string targetPath, long? bytes) {
            return new LfxProgress(LfxProgressType.Expand, sourcePath, targetPath, bytes);
        }

        private readonly LfxProgressType m_type;
        private readonly string m_targetPath;
        private readonly string m_sourcePath;
        private readonly long? m_bytes;

        public LfxProgress(LfxProgressType type, string sourcePath, string targetPath, long? bytes) {
            m_type = type;
            m_targetPath = targetPath;
            m_sourcePath = sourcePath;
            m_bytes = bytes;
        }

        public LfxProgressType Type => m_type;
        public long? Bytes => m_bytes;
        public string TargetPath => m_targetPath;
        public string SourcePath => m_sourcePath;

        public override string ToString() => $"{m_type}, bytes={m_bytes}: {m_sourcePath} => {m_targetPath}";
    }
    public delegate void LfxProgressDelegate(LfxProgress progress);

    public sealed class LfxLoader {
        private delegate string LfxDownloadDelegate(LfxArchiveId hash, string targetPath);
        private delegate Task<string> LfxDownloadAsyncDelegate(LfxArchiveId hash, string targetPath);
        private delegate Task<string> LfxExpandAsyncDelegate(LfxId id, string sourcePath, string targetPath);

        private sealed class LfxContentCache {

            private readonly AsyncSelfLoadingDirectory m_cache;
            private readonly AsyncSelfLoadingDirectory m_archiveCache;
            private readonly AsyncSelfLoadingDirectory m_readOnlyArchiveCache;
            private readonly bool m_canLinkToArchive;
            private readonly bool m_canLinkToReadOnlyArchive;

            internal LfxContentCache(
                string cacheDir,
                string archiveCacheDir,
                string readOnlyArchiveCacheDir = null) {

                m_canLinkToArchive =
                    cacheDir.GetPathRoot().EqualsIgnoreCase(archiveCacheDir.GetPathRoot());
                m_canLinkToReadOnlyArchive =
                    archiveCacheDir.GetPathRoot().EqualsIgnoreCase(readOnlyArchiveCacheDir?.GetPathRoot());

                if (readOnlyArchiveCacheDir != null)
                    m_readOnlyArchiveCache = new AsyncSelfLoadingDirectory(readOnlyArchiveCacheDir, PartitionArchive);

                if (archiveCacheDir != null) {
                    m_archiveCache = new AsyncSelfLoadingDirectory(archiveCacheDir, PartitionArchive);
                    m_archiveCache.OnCopyProgress += (sourcePath, targetPath, bytes) =>
                        RaiseProgressEvent(LfxProgress.Copy(sourcePath, targetPath, bytes));

                    // archive delegates to read-only archive cache, else downloads
                    m_archiveCache.OnTryLoadAsync += async (key, tempPath) => {

                        // try read-only archive cache
                        string lanCachePath = null;
                        if (m_readOnlyArchiveCache?.TryGetPath(key, out lanCachePath) == true)
                            return lanCachePath;

                        // download
                        return await RaiseDownloadEvent((LfxArchiveId)ByteVector.Parse(key), tempPath);
                    };
                }

                m_cache = new AsyncSelfLoadingDirectory(cacheDir, PartitionContent);
                m_cache.OnCopyProgress += (sourcePath, targetPath, bytes) =>
                    RaiseProgressEvent(LfxProgress.Copy(sourcePath, targetPath, bytes));

                // cache delegates to archive cache, then expands
                m_cache.OnTryLoadAsync += async (key, tempPath) => {

                    var id = LfxId.Parse(key);

                    // try bus cache
                    var busCachePath = await m_archiveCache.TryGetOrLoadPathAsync(id.Hash.ToString());
                    if (busCachePath == null)
                        return null;

                    // expand
                    return await RaiseExpandEvent(id, busCachePath, tempPath);
                };
            }

            private async Task<string> RaiseDownloadEvent(LfxArchiveId hash, string tempPath) {
                if (OnDownloadAsync == null)
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
                    OnDownloadAsync?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                    var downloadPath = await o(hash, tempPath);
                    if (downloadPath == null)
                        continue;

                    return downloadPath;
                }

                return null;
            }
            private async Task<string> RaiseExpandEvent(LfxId id, string sourcePath, string tempPath) {
                if (OnExpandAsync == null)
                    return null;

                foreach (LfxExpandAsyncDelegate expand in
                    OnExpandAsync?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                    var expandedPath = await expand(id, sourcePath, tempPath);
                    if (expandedPath == null)
                        continue;

                    return expandedPath;
                }

                return null;
            }
            private void RaiseProgressEvent(LfxProgress progress) {
                OnProgress?.Invoke(progress);
            }
            private bool TryGetArchivePath(LfxArchiveId hash, out string archivePath) {
                var key = hash.ToString();

                if (!m_archiveCache.TryGetPath(key, out archivePath)) {
                    if (m_readOnlyArchiveCache?.TryGetPath(key, out archivePath) == false)
                        return false;
                }

                return true;
            }

            internal event LfxProgressDelegate OnProgress;
            internal event LfxDownloadDelegate OnDownload;
            internal event LfxDownloadAsyncDelegate OnDownloadAsync;
            internal event LfxExpandAsyncDelegate OnExpandAsync;

            internal LfxLoadAction GetExpandActions(LfxType type) {

                // compressed?
                if (type != LfxType.File)
                    return LfxLoadAction.Expand;

                // different partition?
                else if (!m_canLinkToArchive)
                    return LfxLoadAction.Copy;

                // use alias
                return LfxLoadAction.None;
            }
            internal LfxLoadAction GetDownloadActions(LfxArchiveId hash) {

                // no cached archive?
                string archivePath;
                if (!TryGetArchivePath(hash, out archivePath))
                    return LfxLoadAction.Wan;

                // read-only archive cached on file share?
                else if (archivePath.IsUncPath())
                    return LfxLoadAction.Lan;

                // read-only archive cached on separate partition?
                else if (!archivePath.PathRootEquals(m_archiveCache.Dir))
                    return LfxLoadAction.Bus;

                return LfxLoadAction.None;
            }

            internal bool TryGetPath(LfxId id, out string path) {
                return m_cache.TryGetPath(id.ToString(), out path);
            }
            internal string GetTempDownloadPath() => m_archiveCache.GetTempPath();
            internal async Task<string> TryGetOrLoadPathAsync(LfxId id) {
                var result = await m_cache.TryGetOrLoadPathAsync(id.ToString());
                
                // fix corruption whereby cache is not a subset of archiveCache
                await m_archiveCache.TryGetOrLoadPathAsync(id.Hash.ToString());

                return result;
            }
            internal LfxMetadata GetMetadata(LfxId id, LfxArchiveId hash) {

                string path;
                if (!m_cache.TryGetPath(id.ToString(), out path))
                    throw new ArgumentException("Cannot create metadata without content path.");

                string compressedPath;
                if (!m_archiveCache.TryGetPath(hash.ToString(), out compressedPath))
                    throw new ArgumentException("Cannot create metadata without compressed path.");

                return LfxMetadata.Create(hash, path, compressedPath);
            }

            internal void Clean() {
                m_cache.Clean();
                m_archiveCache.Clean();
            }
        }
        private sealed class LfxUrlToArchiveIdCache {

            private readonly ConstDirectory m_archiveCacheDir;
            private readonly ConstDirectory m_readOnlyArchiveCacheDir;

            internal LfxUrlToArchiveIdCache(
                string archiveCacheDir,
                string readOnlyArchiveCacheDir) {

                m_archiveCacheDir = new ConstDirectory(archiveCacheDir, PartitionArchive);
                if (readOnlyArchiveCacheDir != null)
                    m_readOnlyArchiveCacheDir = new ConstDirectory(readOnlyArchiveCacheDir, PartitionArchive);
            }

            internal bool TryGetValue(Uri url, out LfxArchiveId hash) {
                hash = default(LfxArchiveId);
                var urlHash = LfxUrlId.Create(url);

                string path;
                if (!m_archiveCacheDir.TryGetPath(urlHash.ToString(), out path)) {

                    if (m_readOnlyArchiveCacheDir?.TryGetPath(urlHash.ToString(), out path) != true)
                        return false;
                }

                hash = (LfxArchiveId)ByteVector.Load(path);
                return true;
            }
            internal async Task PutAsync(Uri url, LfxArchiveId hash) {
                var urlHash = LfxUrlId.Create(url);
                var idText = hash.ToString();

                await m_archiveCacheDir.EchoText(urlHash.ToString(), idText);
            }

            internal IEnumerable<string> ArchiveCache() => m_archiveCacheDir;
            internal IEnumerable<string> ReadOnlyArchiveCache() => m_readOnlyArchiveCacheDir;

            internal void Clean() {
                m_archiveCacheDir.Clean();
            }
        }
        private sealed class LfxContentIdToInfoCache {

            private readonly ConstDirectory m_cacheDir;

            internal LfxContentIdToInfoCache(
                string cacheDir) {

                m_cacheDir = new ConstDirectory(cacheDir, PartitionContent);
            }

            internal bool TryGetValue(LfxId id, out LfxInfo info) {
                info = default(LfxInfo);

                string infoPath;
                if (!m_cacheDir.TryGetPath(id.ToString(), out infoPath))
                    return false;

                info = LfxInfo.Load(infoPath);
                return true;
            }
            internal async Task PutAsync(LfxId id, LfxInfo info) {
                var idText = id.ToString();
                var infoText = info.ToString();

                await m_cacheDir.EchoText(idText, infoText);
            }

            internal IEnumerable<string> CacheContent() => m_cacheDir;

            internal void Clean() {
                m_cacheDir.Clean();
            }
        }

        private const int L1PartitionCount = 2;
        private const int L2PartitionCount = 2;
        private const int MinimumHashLength = L1PartitionCount + L2PartitionCount;

        public const string UrlHashToArchiveIdDirName = "urlHashToArchiveId";
        public const string ContentIdToInfo = "contentIdToInfo";
        public const string CompressedDirName = "archive";
        public const string ExpandedDirName = "content";

        private static string PartitionContent(string key) {
            var id = LfxId.Parse(key);

            return Path.Combine(
                id.Type.ToString(), 
                id.Version.ToString(), 
                PartitionArchive(id.Hash.ToString())
           );
        }
        private static string PartitionArchive(string hash) {

            if (hash.Length < MinimumHashLength)
                throw new ArgumentException(
                    $"Hash '{hash}' must be at least '{MinimumHashLength}' characters long.");

            return Path.Combine(
                hash.ToString().Substring(0, L1PartitionCount),
                hash.ToString().Substring(L1PartitionCount, L2PartitionCount),
                hash
            );
        }
        private static async Task<LfxArchiveId> DownloadAsync(Uri url, string targetPath, Action<LfxProgress> onProgress) {

            try {
                var byteHash = await url.DownloadAndHash(
                    tempPath: targetPath,
                    onProgress: progress =>
                        onProgress(LfxProgress.Download(url, targetPath, progress))
                );

                return (LfxArchiveId)ByteVector.Create(byteHash);

            } catch (WebException e) {
                throw new WebException($"Failed to download {url}.", e);
            }
        }

        private readonly ConcurrentDictionary<LfxArchiveId, Uri> m_knownArchives;
        private readonly ConcurrentDictionary<LfxId, LfxPointer> m_knownContent;
        private readonly LfxContentCache m_contentCache;
        private readonly LfxUrlToArchiveIdCache m_urlToArchiveIdCache;
        private readonly LfxContentIdToInfoCache m_contentIdToInfoCache;

        public LfxLoader(
            string diskCacheDir,
            string busCacheDir,
            string lanCacheDir = null) {

            if (lanCacheDir?.EqualPath(busCacheDir) == true)
                throw new ArgumentNullException( // LanCacheDir is readonly
                    $"LanCacheDir '{lanCacheDir}' cannot equal BusCacheDir '{busCacheDir}'.");

            m_knownArchives = new ConcurrentDictionary<LfxArchiveId, Uri>();
            m_knownContent = new ConcurrentDictionary<LfxId, LfxPointer>();

            m_contentCache = new LfxContentCache(
                cacheDir: diskCacheDir.PathCombine(ExpandedDirName),
                archiveCacheDir: busCacheDir.PathCombine(CompressedDirName),
                readOnlyArchiveCacheDir: lanCacheDir?.PathCombine(CompressedDirName)
            );
            m_contentCache.OnDownloadAsync += async (archiveId, targetPath) => {

                // lookup pointer
                var url = m_knownArchives[archiveId];

                // download!
                var downloadId = await DownloadAsync(url, targetPath, RaiseProgressEvent);

                // verify hash
                if (archiveId != null && archiveId != downloadId)
                    throw new Exception(
                        $"Downloaded url '{url}' content hash '{downloadId}' " +
                        $"is different than expected hash '{archiveId}'.");

                return targetPath;
            };
            m_contentCache.OnExpandAsync += async (id, compressedPath, tempDir) => {

                var pointer = m_knownContent[id];

                // cache file
                if (pointer.IsFile)
                    return compressedPath;

                // expand zip
                if (pointer.IsZip || pointer.IsNuget) {

                    // inject nuget shims
                    if (pointer.IsNuget) {
                        WriteResource($"nuget.v{pointer.Version}", "shim.targets", compressedPath, tempDir);
                        WriteResource($"nuget.v{pointer.Version}", "shim.props", compressedPath, tempDir);
                    }

                    // decompress
                    await compressedPath.ExpandZip(tempDir, bytes =>
                        RaiseProgressExpandEvent(compressedPath, tempDir, bytes)
                    );
                }

                // expand exe
                else if (pointer.IsExe)
                    await compressedPath.ExpandExe(tempDir, pointer.Args, bytes =>
                        RaiseProgressExpandEvent(compressedPath, tempDir, bytes)
                    );

                else
                    throw new Exception($"Could not expand unreconginzed pointer '{pointer}'.");

                return tempDir;
            };
            m_contentCache.OnProgress += RaiseProgressEvent;

            m_contentIdToInfoCache = new LfxContentIdToInfoCache(
                diskCacheDir.PathCombine(ContentIdToInfo)
            );

            m_urlToArchiveIdCache = new LfxUrlToArchiveIdCache(
                archiveCacheDir: busCacheDir.PathCombine(UrlHashToArchiveIdDirName),
                readOnlyArchiveCacheDir: lanCacheDir?.PathCombine(UrlHashToArchiveIdDirName)
            );  
        }

        private void WriteResource(string resourcePath, string resourceName, string compressedPath, string targetDir) {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceId = $"{GetType().Namespace}.resources.{resourcePath}.{resourceName}";

            Directory.CreateDirectory(targetDir);

            // open resource
            using (var stream = assembly.GetManifestResourceStream(resourceId)) {

                // open target
                using (var sw = File.Open(Path.Combine(targetDir, resourceName), FileMode.CreateNew))
                    stream.CopyTo(sw);

                // report progress
                RaiseProgressExpandEvent(compressedPath, targetDir, stream.Length);
            }
        }
        private IEnumerable<LfxContent> Cache(IEnumerable<string> paths) {
            foreach (var path in paths) {
                var info = LfxInfo.Load(path);
                yield return LfxContent.Create(info, path);
            }
        }
        private async Task<LfxContent> GetOrLoadContentAsync(LfxPointer pointer, LfxArchiveId hash) {
            LfxInfo info;
            var url = pointer.Url;

            var id = new LfxId(pointer.Type, pointer.Version, hash);

            // register key -> info needed to load
            m_knownArchives.GetOrAdd(hash, pointer.Url);
            m_knownContent.GetOrAdd(id, pointer);

            // get or self-load content!
            var contentPath = await m_contentCache.TryGetOrLoadPathAsync(id);

            // cache url -> hash
            await m_urlToArchiveIdCache.PutAsync(url, hash);

            // cache info
            if (!m_contentIdToInfoCache.TryGetValue(id, out info)) {
                info = LfxInfo.Create(pointer, m_contentCache.GetMetadata(id, hash));
                await m_contentIdToInfoCache.PutAsync(id, info);
            }

            return LfxContent.Create(info, contentPath);
        }

        // events
        private void RaiseProgressExpandEvent(string sourcePath, string targetPath, long? bytes) {
            RaiseProgressEvent(LfxProgress.Expand(sourcePath, targetPath, bytes));
        }
        private void RaiseProgressEvent(LfxProgress progress) {
            OnProgress?.Invoke(progress);
        }
        public event LfxProgressDelegate OnProgress;

        // fetch
        public bool TryGetArchiveId(Uri url, out LfxArchiveId hash) {
            return m_urlToArchiveIdCache.TryGetValue(url, out hash);
        }
        public bool TryGetContent(LfxInfo info, out LfxContent content) {
            content = default(LfxContent);

            if (!info.HasMetadata) {

                // url -> hash
                LfxArchiveId hash;
                if (!TryGetArchiveId(info.Url, out hash))
                    return false;

                // hash -> id -> info (with metadata)
                var id = new LfxId(info.Type, info.Version, hash);
                if (!m_contentIdToInfoCache.TryGetValue(id, out info))
                    return false;
            }

            // id -> contentPath
            string contentPath;
            if (!m_contentCache.TryGetPath((LfxId)info.Id, out contentPath))
                return false;

            // contentPath + full info
            content = new LfxContent(info, contentPath);
            return true;
        }
        public async Task<LfxContent> GetOrLoadContentAsync(LfxPointer pointer) {
            var url = pointer.Url;

            // discover hash using url?
            LfxArchiveId hash;
            if (TryGetArchiveId(url, out hash))
                return await GetOrLoadContentAsync(pointer, hash);

            // eager download
            var downloadPath = m_contentCache.GetTempDownloadPath();
            hash = await DownloadAsync(url, downloadPath, RaiseProgressEvent);

            // prevent self-loading cache from re-downloading content
            LfxDownloadDelegate downloadDelegate = null;
            m_contentCache.OnDownload += downloadDelegate = (_expectedHash, _downloadPath) => {

                // return freshly downloaded content for this hash
                if (_expectedHash == hash)
                    return downloadPath;

                return null;
            };

            try {
                return await GetOrLoadContentAsync(pointer, hash);
            } finally {
                m_contentCache.OnDownload -= downloadDelegate;
            }
        }
        public Task<LfxContent> GetOrLoadContentAsync(LfxInfo info) {
            var pointer = info.Pointer;

            if (!info.HasMetadata)
                return GetOrLoadContentAsync(pointer);

            return GetOrLoadContentAsync(pointer, (LfxArchiveId)info.Hash);
        }

        // reflection (url)
        public IEnumerable<LfxInfo> GetInfos(LfxArchiveId hash) {

            // content maps 1-n in the different types of ways it might be expanded
            var types = new[] {
                LfxType.File,
                LfxType.Zip,
                LfxType.Exe,
                LfxType.Nuget,
            };

            var versions = Enumerable.Range(1, 1);

            foreach (var version in versions) {
                foreach (var type in types) {
                    LfxInfo info;
                    var id = new LfxId(type, version, hash);
                    if (m_contentIdToInfoCache.TryGetValue(id, out info))
                        yield return info;
                }
            }
        }
        public LfxLoadAction GetLoadAction(LfxInfo info) {

            // cached?
            LfxContent content;
            if (TryGetContent(info, out content))
                return LfxLoadAction.None;

            // archive cached?
            var downloadActions = LfxLoadAction.Wan;
            LfxArchiveId hash;
            if (info.HasMetadata && TryGetArchiveId(info.Url, out hash)) {
                if (info.HasMetadata)
                    hash = (LfxArchiveId)info.Hash;

                downloadActions = m_contentCache.GetDownloadActions(hash);
            }

            return downloadActions | m_contentCache.GetExpandActions(info.Type);
        }
        public IEnumerable<LfxCount> GetLoadEffort(
            IEnumerable<LfxPath> paths, 
            out IEnumerable<LfxPath> pathsWithoutMetadata) {

            // deduplicate urls and references to the same content
            var urls = new HashSet<LfxUrlId>();
            var ids = new HashSet<LfxId>();

            var copyCount = new LfxCount(LfxLoadAction.Copy);
            var expandCount = new LfxCount(LfxLoadAction.Expand);
            var busCount = new LfxCount(LfxLoadAction.Bus);
            var lanCount = new LfxCount(LfxLoadAction.Lan);
            var wanCount = new LfxCount(LfxLoadAction.Wan);

            var pathsWithoutMetadataList = new List<LfxPath>();

            foreach (var path in paths.Where(o => o.IsContent)) {
                var info = (LfxInfo)path.Info;

                if (!info.HasMetadata) {
                    pathsWithoutMetadataList.Add(path);
                    continue;
                }

                var loadAction = GetLoadAction(info);

                // expand
                var expandAction = loadAction & LfxLoadAction.ExpandMask;
                if (expandAction != 0 && !ids.Contains((LfxId)info.Id)) {
                    ids.Add((LfxId)info.Id);
                    var count = new LfxCount(expandAction, (long)path.Size);

                    switch (expandAction) {
                        case LfxLoadAction.Copy: copyCount += count; break;
                        case LfxLoadAction.Expand: expandCount += count; break;
                    }
                }

                // download
                var downloadAction = loadAction & LfxLoadAction.DownloadMask;
                if (downloadAction != 0 && !urls.Contains(info.UrlHash)) {
                    urls.Add(info.UrlHash);
                    var count = new LfxCount(downloadAction, (long)path.DownloadSize);

                    switch (downloadAction) {
                        case LfxLoadAction.Bus: busCount += count; break;
                        case LfxLoadAction.Lan: lanCount += count; break;
                        case LfxLoadAction.Wan: wanCount += count; break;
                    }
                }
            }

            pathsWithoutMetadata = pathsWithoutMetadataList;

            return new[] {
                copyCount,
                expandCount,
                busCount,
                lanCount,
                wanCount
            };
        }

        // reflection (enumeration)
        public IEnumerable<LfxContent> Content() {
            foreach (var path in m_contentIdToInfoCache.CacheContent()) {
                var info = LfxInfo.Load(path);
                yield return LfxContent.Create(info, path);
            }
        }

        // housekeeping
        public void Clean() {
            m_contentCache.Clean();
            m_urlToArchiveIdCache.Clean();
            m_contentIdToInfoCache.Clean();
        }
    }
}