using System.IO;
using Util;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Net;

namespace Git.Lfx {

    [Flags]
    public enum LfxProgressType {
        None = 0,
        Download = 1 << 0,
        Copy = 1 << 2,
        Expand = 1 << 3,
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
        private delegate Task<string> LfxExpandAsyncDelegate(LfxContentId id, string sourcePath, string targetPath);
        private sealed class LfxContentCache {

            private readonly AsyncSelfLoadingDirectory m_diskCache;
            private readonly AsyncSelfLoadingDirectory m_busCache;
            private readonly AsyncSelfLoadingDirectory m_lanCache;

            internal LfxContentCache(
                string diskCacheDir,
                string busCacheDir,
                string lanCacheDir = null) {

                if (lanCacheDir != null) {
                    m_lanCache = new AsyncSelfLoadingDirectory(lanCacheDir, PartitionArchive);
                }

                if (busCacheDir != null) {
                    m_busCache = new AsyncSelfLoadingDirectory(busCacheDir, PartitionArchive);
                    m_busCache.OnCopyProgress += (sourcePath, targetPath, bytes) =>
                        RaiseProgressEvent(LfxProgress.Copy(sourcePath, targetPath, bytes));

                    // busCache delegates to lanCache, else downloads
                    m_busCache.OnTryLoadAsync += async (key, tempPath) => {

                        // try lan cache
                        string lanCachePath = null;
                        if (m_lanCache?.TryGetPath(key, out lanCachePath) == true)
                            return lanCachePath;

                        // download
                        return await RaiseDownloadEvent(LfxArchiveId.Parse(key), tempPath);
                    };
                }

                m_diskCache = new AsyncSelfLoadingDirectory(diskCacheDir, PartitionContent);
                m_diskCache.OnCopyProgress += (sourcePath, targetPath, bytes) =>
                    RaiseProgressEvent(LfxProgress.Copy(sourcePath, targetPath, bytes));

                // diskCache delegates to busCache, then expands
                m_diskCache.OnTryLoadAsync += async (key, tempPath) => {

                    var contentId = LfxContentId.Parse(key);

                    // try bus cache
                    var busCachePath = await m_busCache.TryGetOrLoadPathAsync(contentId.Hash);
                    if (busCachePath == null)
                        return null;

                    // expand
                    return await RaiseExpandEvent(contentId, busCachePath, tempPath);
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
            private async Task<string> RaiseExpandEvent(LfxContentId id, string sourcePath, string tempPath) {
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

            internal event LfxProgressDelegate OnProgress;
            internal event LfxDownloadDelegate OnDownload;
            internal event LfxDownloadAsyncDelegate OnDownloadAsync;
            internal event LfxExpandAsyncDelegate OnExpandAsync;

            internal bool TryGetPath(LfxArchiveId hash, out string path) {
                return m_diskCache.TryGetPath(hash, out path);
            }
            internal async Task<string> TryGetOrLoadContentPathAsync(LfxContentId id) {
                return await m_diskCache.TryGetOrLoadPathAsync(id);
            }
            internal async Task<string> TryGetOrLoadArchivePathAsync(LfxArchiveId id) {
                return await m_busCache.TryGetOrLoadPathAsync(id);
            }
            internal string GetTempDownloadPath() => m_busCache.GetTempPath();

            internal void Clean() {
                m_diskCache.Clean();
                m_busCache.Clean();
            }
        }
        private sealed class LfxUrlToArchiveIdCache {
            public static LfxArchiveId GetUrlHash(Uri url) => LfxArchiveId.Create(url.ToString().ToLower(), Encoding.UTF8);

            private readonly ImmutableDirectory m_busCacheDir;
            private readonly ImmutableDirectory m_lanCacheDir;

            internal LfxUrlToArchiveIdCache(
                string busCacheDir,
                string lanCacheDir) {

                m_busCacheDir = new ImmutableDirectory(busCacheDir, PartitionArchive);
                if (lanCacheDir != null)
                    m_lanCacheDir = new ImmutableDirectory(lanCacheDir, PartitionArchive);
            }

            internal bool TryGetValue(Uri url, out LfxArchiveId id) {
                id = default(LfxArchiveId);
                var urlHash = GetUrlHash(url);

                string idPath;
                if (!m_busCacheDir.TryGetPath(urlHash, out idPath)) {

                    if (m_lanCacheDir?.TryGetPath(urlHash, out idPath) != true)
                        return false;
                }

                id = LfxArchiveId.Load(idPath);
                return true;
            }
            internal async Task PutAsync(Uri url, LfxArchiveId id) {
                var urlHash = GetUrlHash(url);
                var idText = id.ToString();

                await m_busCacheDir.EchoText(urlHash, idText);
            }

            internal IEnumerable<string> BusCache() => m_busCacheDir;
            internal IEnumerable<string> LanCache() => m_lanCacheDir;

            internal void Clean() {
                m_busCacheDir.Clean();
            }
        }
        private sealed class LfxContentIdToInfoCache {

            private readonly ImmutableDirectory m_diskCacheDir;

            internal LfxContentIdToInfoCache(
                string diskCacheDir) {

                m_diskCacheDir = new ImmutableDirectory(diskCacheDir, PartitionContent);
            }

            internal bool TryGetValue(LfxContentId id, out LfxInfo info) {
                info = default(LfxInfo);

                string infoPath;
                if (!m_diskCacheDir.TryGetPath(id, out infoPath))
                    return false;

                info = LfxInfo.Load(infoPath);
                return true;
            }
            internal async Task PutAsync(LfxContentId id, LfxInfo info) {
                var idText = id.ToString();
                var infoText = info.ToString();

                await m_diskCacheDir.EchoText(idText, infoText);
            }

            internal IEnumerable<string> DiskCache() => m_diskCacheDir;

            internal void Clean() {
                m_diskCacheDir.Clean();
            }
        }

        private const int L1PartitionCount = 2;
        private const int L2PartitionCount = 2;
        private const int MinimumHashLength = L1PartitionCount + L2PartitionCount;

        private static string PartitionContent(string key) {
            var id = LfxContentId.Parse(key);
            return Path.Combine(id.Type.ToString(), id.Version.ToString(), PartitionArchive(id.Hash));
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

                // finish
                onProgress(LfxProgress.Download(url, targetPath, null));

                return LfxArchiveId.Create(byteHash);

            } catch (WebException e) {
                throw new WebException($"Failed to download {url}.", e);
            }
        }

        public const string UrlHashToArchiveIdDirName = "urlHashToArchiveId";
        public const string ContentIdToInfo = "contentIdToInfo";
        public const string CompressedDirName = "archive";
        public const string ExpandedDirName = "content";

        private readonly ConcurrentDictionary<LfxArchiveId, Uri> m_knownArchives;
        private readonly ConcurrentDictionary<LfxContentId, LfxPointer> m_knownContent;
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
            m_knownContent = new ConcurrentDictionary<LfxContentId, LfxPointer>();

            m_contentCache = new LfxContentCache(
                diskCacheDir: diskCacheDir.PathCombine(ExpandedDirName),
                busCacheDir: busCacheDir.PathCombine(CompressedDirName),
                lanCacheDir: lanCacheDir?.PathCombine(CompressedDirName)
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
                    await compressedPath.ExpandZip(tempDir, bytes =>
                        RaiseProgressEvent(LfxProgress.Expand(compressedPath, tempDir, bytes)));

                    // nuget shims
                    if (pointer.IsNuget) {
                        WriteResource($"nuget.v{pointer.Version}", "shim.targets", tempDir);
                        WriteResource($"nuget.v{pointer.Version}", "shim.props", tempDir);
                    }
                }

                // expand exe
                else if (pointer.IsExe)
                    await compressedPath.ExpandExe(tempDir, pointer.Args, bytes =>
                        RaiseProgressEvent(LfxProgress.Expand(compressedPath, tempDir, bytes)));

                else
                    throw new Exception($"Could not expand unreconginzed pointer '{pointer}'.");

                // finished
                RaiseProgressEvent(LfxProgress.Expand(compressedPath, tempDir, null));

                return tempDir;
            };
            m_contentCache.OnProgress += RaiseProgressEvent;

            m_contentIdToInfoCache = new LfxContentIdToInfoCache(
                diskCacheDir.PathCombine(ContentIdToInfo)
            );

            m_urlToArchiveIdCache = new LfxUrlToArchiveIdCache(
                busCacheDir: busCacheDir.PathCombine(UrlHashToArchiveIdDirName),
                lanCacheDir: lanCacheDir?.PathCombine(UrlHashToArchiveIdDirName)
            );  
        }

        private void WriteResource(string resourcePath, string resourceName, string targetDir) {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceId = $"{GetType().Namespace}.resources.{resourcePath}.{resourceName}";
            using (var stream = assembly.GetManifestResourceStream(resourceId))
                using (var sw = File.Open(Path.Combine(targetDir, resourceName), FileMode.CreateNew))
                    stream.CopyTo(sw);
        }

        // events
        private void RaiseProgressEvent(LfxProgress progress) {
            OnProgress?.Invoke(progress);
        }
        public event LfxProgressDelegate OnProgress;

        // fetch
        public async Task<LfxEntry> GetOrLoadEntryAsync(LfxPointer pointer, LfxArchiveId? expectedArchiveId = null) {
            LfxInfo info;
            LfxArchiveId archiveId;
            var url = pointer.Url;

            if (expectedArchiveId == null) {

                // discover archiveId using url?
                LfxArchiveId _archiveId;
                if (m_urlToArchiveIdCache.TryGetValue(url, out _archiveId))
                    return await GetOrLoadEntryAsync(pointer, _archiveId);

                // eager download
                var downloadPath = m_contentCache.GetTempDownloadPath();
                archiveId = await DownloadAsync(url, downloadPath, RaiseProgressEvent);

                // prevent self-loading cache from re-downloading content
                LfxDownloadDelegate downloadDelegate = null;
                m_contentCache.OnDownload += downloadDelegate = (_expectedHash, _downloadPath) => {

                    // return freshly downloaded content for this hash
                    if (_expectedHash == archiveId)
                        return downloadPath;

                    return null;
                };

                try {
                    return await GetOrLoadEntryAsync(pointer, archiveId);
                } finally {
                    m_contentCache.OnDownload -= downloadDelegate;
                }
            }

            archiveId = expectedArchiveId.Value;
            var contentId = new LfxContentId(pointer.Type, pointer.Version, archiveId);

            // register hash -> pointer
            m_knownArchives.GetOrAdd(archiveId, pointer.Url);
            m_knownContent.GetOrAdd(contentId, pointer);

            // get or self-load content
            var contentPath = await m_contentCache.TryGetOrLoadContentPathAsync(contentId);

            // metadata cached?
            if (!m_contentIdToInfoCache.TryGetValue(contentId, out info)) {

                // get compressed content
                var compressedPath = await m_contentCache.TryGetOrLoadArchivePathAsync(archiveId);

                // compute metadata
                var metadata = LfxMetadata.Create(archiveId, contentPath, compressedPath);

                // create info
                info = LfxInfo.Create(pointer, metadata);

                // cache info
                await m_contentIdToInfoCache.PutAsync(contentId, info);
            }

            return LfxEntry.Create(info, contentPath);
        }
        public IEnumerable<LfxInfo> GetInfos(Uri url) {
            // a url maps 1-1 to a hash of its content
            LfxArchiveId archiveId;
            if (!m_urlToArchiveIdCache.TryGetValue(url, out archiveId))
                yield break;

            // content maps 1-n in the different types of ways it might be expanded
            var types = new[] {
                LfxPointerType.File,
                LfxPointerType.Zip,
                LfxPointerType.Exe,
                LfxPointerType.Nuget,
            };

            var versions = Enumerable.Range(1, 1);

            foreach (var version in versions) {
                foreach (var type in types) {
                    LfxInfo info;
                    var contentId = new LfxContentId(type, version, archiveId);
                    if (m_contentIdToInfoCache.TryGetValue(contentId, out info))
                        yield return info;
                }
            }
        }
        public string GetUrlHash(Uri url) => LfxUrlToArchiveIdCache.GetUrlHash(url);

        // housekeeping
        public void Clean() {
            m_contentCache.Clean();
            m_urlToArchiveIdCache.Clean();
        }

        // reflection
        private IEnumerable<LfxEntry> Cache(IEnumerable<string> paths) {
            foreach (var path in paths) {
                var info = LfxInfo.Load(path);
                yield return LfxEntry.Create(info, path);
            }
        }
        public IEnumerable<LfxEntry> DiskCache() {
            foreach (var path in m_contentIdToInfoCache.DiskCache()) {
                var info = LfxInfo.Load(path);
                yield return LfxEntry.Create(info, path);
            }
        }
        public IEnumerable<LfxEntry> BusCache() => Cache(m_urlToArchiveIdCache.BusCache());
        public IEnumerable<LfxEntry> LanCache() => Cache(m_urlToArchiveIdCache.LanCache());
    }
}