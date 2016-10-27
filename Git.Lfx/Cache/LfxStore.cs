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

    public abstract class LfxCache : IDisposable {
        public const string LfxCacheDirName = @"lfx";
        public const string PointerDirName = "pointers";
        public const string UrlsDirName = "urls";
        public const string CompressedDirName = "compressed";
        public const string ExpandedDirName = "expanded";
        public const string TempDirName = "temp";
        public static readonly string DefaultUserCacheDir = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            LfxCacheDirName
        ).ToDir();

        public static LfxCache CreateCache(
            string diskCacheDir = null,
            string busCacheDir = null,
            string lanCacheDir = null,
            string workingDir = null) {

            LfxCache lanCache = null;
            LfxCache busCache = null;

            // default working dir is temp dir
            if (workingDir == null)
                workingDir = Path.GetTempPath().GetDir();

            // readonly (aka "lan") cache
            if (lanCacheDir != null)
                lanCache = new LfxReadOnlyCache(
                    Path.Combine(lanCacheDir, CompressedDirName)
                );

            // download (aka "bus") cache
            if (busCacheDir != null)
                busCache = new LfxDownloadCache(
                    Path.Combine(busCacheDir, CompressedDirName),
                    Path.Combine(busCacheDir, TempDirName, Path.GetRandomFileName()),
                    lanCache
                );

            // default diskCacheDir
            if (diskCacheDir == null) {

                // if working dir partition equals %APPDATA% partition then put cache in %APPDATA%
                var workingDirRoot = Path.GetPathRoot(workingDir);
                if (workingDirRoot.EqualsIgnoreCase(Path.GetPathRoot(DefaultUserCacheDir)))
                    diskCacheDir = DefaultUserCacheDir;

                // else put cache at root of working dir
                else
                    diskCacheDir = Path.Combine(workingDirRoot, LfxCacheDirName);
            }

            // default download cache
            if (busCache == null)
                busCache = new LfxDownloadCache(
                    Path.Combine(diskCacheDir, CompressedDirName),
                    Path.Combine(diskCacheDir, TempDirName, Path.GetRandomFileName()),
                    lanCache
                );

            // expanded (aka "disk") cache
            var cache = new LfxExpandedCache(
                Path.Combine(diskCacheDir, ExpandedDirName),
                Path.Combine(diskCacheDir, TempDirName, Path.GetRandomFileName()),
                busCache
            );

            return cache;
        }

        private readonly LfxCache m_parent;
        private readonly ImmutableAsyncDictionary<LfxHash, LfxPointer, string> m_asyncDictionary;
        private readonly ImmutablePathDictionary m_contentByHash;
        private readonly ImmutablePathDictionary m_pointerByHash;
        private readonly ImmutablePathDictionary m_pointerByUrl;

        protected LfxCache(
            string dir,
            string tempDir,
            LfxCache parent) {

            m_parent = parent;

            m_contentByHash = new ImmutablePathDictionary(dir);
            m_contentByHash.OnCopyProgress += ReportCopyProgress;

            m_pointerByHash = new ImmutablePathDictionary(Path.Combine(dir, PointerDirName));
            m_pointerByUrl = new ImmutablePathDictionary(Path.Combine(dir, UrlsDirName));

            m_asyncDictionary = new ImmutableAsyncDictionary<LfxHash, LfxPointer, string>(
                getKey: pointer => pointer.Hash,
                tryLoadValue: key => {
                    string value;
                    var success = m_contentByHash.TryGetValue(key, out value);
                    return new Tuple<bool, string>(success, value);
                },
                loadValueAsync: GetOrLoadValueAsync
            );
        }

        private void ReportCopyProgress(long progress) => ReportProgress(LfxProgressType.Copy, progress);
        private void ReportProgress(LfxProgressType type, long progress) {
            OnProgress?.Invoke(type, progress);
        }
        private LfxPointer CreateFullPointer(LfxPointer partialPointer, string cachePath) {
            var url = partialPointer.Url;
            var hash = LfxHash.Parse(Path.GetFileName(cachePath));
            var fileSize = cachePath.GetFileSize();

            // file
            if (partialPointer.IsFile)
                return LfxPointer.CreateFile(url, fileSize, hash);

            // archive
            string archivePath;
            m_parent.m_asyncDictionary.TryGetValue(hash, out archivePath);
            fileSize = archivePath.GetFileSize();

            var dirSize = cachePath.GetDirectorySize();

            // exe
            if (partialPointer.IsExe)
                return LfxPointer.CreateExe(url, fileSize, hash, dirSize, partialPointer.Args);

            // zip
            return LfxPointer.CreateZip(url, fileSize, hash, dirSize);
        }

        protected ImmutablePathDictionary Store => m_contentByHash;
        protected void ReportDownloadProgress(long progress) => ReportProgress(LfxProgressType.Download, progress);
        protected void ReportExpansionProgress(long progress) => ReportProgress(LfxProgressType.Expand, progress);

        protected abstract Task<string> GetOrLoadValueAsync(LfxPointer pointer);

        public LfxCache Parent => m_parent;
        public string CacheDir => m_contentByHash.ToString();
        public string TempDir => m_contentByHash.TempDir;
        public event Action<LfxProgressType, long> OnProgress;
        public async Task<LfxPointer> CreatePointer(LfxPointer pointer) {

            if (IsCompressed)
                throw new InvalidOperationException(
                    $"Cache '{this}' is compressed and so cannot create a poitner.");

            var urlLower = pointer.Url.ToString().ToLower();
            var urlHash = LfxHash.Compute(urlLower, Encoding.UTF8);

            // try url cache
            string pointerPath;
            if (m_pointerByUrl.TryGetValue(urlHash, out pointerPath))
                return LfxPointer.Load(pointerPath);

            // resolve pointer!
            var cachePath = await ResolvePointer(pointer);
            if (cachePath == null)
                throw new ArgumentException(
                    $"Failed to create pointer '{pointer}'.");

            // flesh out pointer; fill in hash and sizes
            pointer = CreateFullPointer(pointer, cachePath);

            // write pointer metadata
            await m_pointerByHash.PutText(pointer.Value, pointer.Hash);

            // write url metadata
            await m_pointerByUrl.PutText(pointer.Value, urlHash);

            return pointer;
        }
        public async Task<string> ResolvePointer(LfxPointer pointer) {
            return await m_asyncDictionary.GetOrLoadValueAsync(pointer);
        }
        public virtual bool IsCompressed => false;
        public virtual bool IsReadOnly => false;

        public void Dispose() {
            m_contentByHash?.Dispose();
        }

        public override string ToString() => m_contentByHash.ToString();
    }
    internal sealed class LfxExpandedCache : LfxCache {

        public LfxExpandedCache(
            string dir,
            string tempDir,
            LfxCache parent) : base(dir, tempDir, parent) {

            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
        }

        protected override async Task<string> GetOrLoadValueAsync(LfxPointer pointer) {

            // get compressed file
            var compressedPath = await Parent.ResolvePointer(pointer);
            if (compressedPath == null)
                throw new ArgumentException(
                    $"Url '{pointer.Url}' failed to resolve.");

            // cache path
            var hash = Path.GetFileName(compressedPath);

            // copy file or hard link to file
            if (pointer.IsFile)
                return await Store.Put(compressedPath, hash, preferHardLink: true);

            // expand archive into directory
            var tempDir = Path.Combine(TempDir, Path.GetTempFileName());

            // zip
            if (pointer.IsZip)
                await compressedPath.ExpandZip(tempDir, ReportExpansionProgress);

            // exe
            else if (pointer.IsExe)
                await compressedPath.ExpandExe(tempDir, pointer.Args, ReportExpansionProgress);

            // move directory
            return await Store.Take(tempDir, hash);
        }
    }
    internal sealed class LfxDownloadCache : LfxCache {

        public LfxDownloadCache(
            string dir,
            string tempDir,
            LfxCache parent) : base(dir, tempDir, parent) {
        }

        public override bool IsCompressed => true;
        protected override async Task<string> GetOrLoadValueAsync(LfxPointer pointer) {

            // try parent
            if (Parent != null) {
                var parentPath = await Parent.ResolvePointer(pointer);
                if (parentPath != null)
                    return parentPath;
            }

            // download to temp incase of download fails
            var tempPath = Path.Combine(TempDir, Path.GetRandomFileName());
            var sha256 = SHA256.Create();

            // save to temp file
            using (var fileStream = File.OpenWrite(tempPath)) {

                // compute hash while saving file
                using (var shaStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write)) {

                    // initiate download
                    using (var downloadStream = await new WebClient().OpenReadTaskAsync(pointer.Url)) {

                        // save downloaded file to temp file while reporting progress
                        await downloadStream.CopyToAsync(shaStream, onProgress: ReportDownloadProgress);
                    }
                }
            }

            // verify hash (unless hash is unknown)
            var hash = LfxHash.Create(sha256.Hash);
            if (!pointer.Hash.IsNull && pointer.Hash != hash)
                throw new Exception($"Downloaded url '{pointer.Url}' hash '{hash}' is different than expected hash '{pointer.Hash}'.");

            // stash compressed file
            return await Store.Take(tempPath, hash);
        }
    }
    internal sealed class LfxReadOnlyCache : LfxCache {
        public LfxReadOnlyCache(string dir) : base(dir, null, null) { }

        public override bool IsCompressed => true;
        public override bool IsReadOnly => true;
        protected override Task<string> GetOrLoadValueAsync(LfxPointer pointer) {
            return Task.FromResult(default(string));
        }
    }
}