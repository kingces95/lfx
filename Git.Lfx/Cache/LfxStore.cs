using System.Collections.Generic;
using System.IO;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using System.Linq;
using System.Collections;
using Util;
using System.Threading.Tasks;
using System;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;

namespace Git.Lfx {

    public sealed class LfxStore : IEnumerable<LfxTarget> {
        private readonly string m_dir;

        public LfxStore(string dir) {
            m_dir = dir.ToDir();
            IODirectory.CreateDirectory(m_dir);
        }

        private string GetPath(LfxHash hash) {
            return Path.Combine(
                m_dir,
                hash.ToString().Substring(0, 2),
                hash.ToString().Substring(2, 2),
                hash
            );
        }
        private IEnumerable<string> Files => IODirectory.GetFiles(m_dir, "*", SearchOption.AllDirectories);
        private IEnumerable<string> Directories => IODirectory.GetDirectories(m_dir)
            .SelectMany(o => IODirectory.GetDirectories(o))
            .SelectMany(o => IODirectory.GetDirectories(o));
        private IEnumerable<string> Targets => Files.Concat(Directories);

        public string Directory => m_dir;
        public LfxTarget Add(LfxHash hash, string path) {

            var cachePath = GetPath(hash);
            IODirectory.CreateDirectory(Path.GetDirectoryName(cachePath));

            // take ownership of file
            if (IOFile.Exists(path)) {
                IOFile.Move(path, cachePath);
            }

            // take ownership of directory
            else {
                IODirectory.Move(path, cachePath);
            }

            return new LfxTarget(this, hash, cachePath);
        }
        public bool TryGet(LfxHash hash, out LfxTarget target) {
            target = default(LfxTarget);
            var path = GetPath(hash);
            if (!IOFile.Exists(path) && !IODirectory.Exists(path))
                return false;
            target = new LfxTarget(this, hash, path);
            return true;
        }
        public bool Contains(LfxHash hash) {
            LfxTarget target;
            return TryGet(hash, out target);
        }
        public int Count => Targets.Count();
        public long Size => Targets.Sum(o => new FileInfo(o).Length);
        public void Clear() => IODirectory.Delete(m_dir, recursive: true);

        public override string ToString() => $"{m_dir}";

        public IEnumerator<LfxTarget> GetEnumerator() {
            return Targets
                .Select(o => new LfxTarget(this, LfxHash.Parse(Path.GetFileName(o)), o))
                .GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Flags]
    public enum LfxStoreFlags {
        None = 0,
        ReadOnly = 1 << 0,
        Expanded = 1 << 1
    }
    public sealed class LfxDirectoryCache :
        ImmutableAsyncDictionary<LfxHash, LfxPointer, string>,
        IDisposable {

        // _directory structure_
        // root
        //   temp
        //     [random]
        //   compressed
        //     [0:2) of hash
        //       [2:4) of hash
        //   expanded
        //     [0:2) of hash
        //       [2:4) of hash

        private const string TempDirName = "temp";
        private const string CompressedDirName = "compressed";
        private const string ExpandedDirName = "expanded";

        private readonly string m_rootDir;
        private readonly TempDir m_tempDir;
        private readonly string m_compressedDir;
        private readonly string m_expandedDir;
        private readonly LfxDirectoryCache m_parent;
        private readonly LfxStoreFlags m_flags;

        public LfxDirectoryCache(
            string rootDir,
            LfxStoreFlags flags = LfxStoreFlags.None, 
            LfxDirectoryCache parent = null) {

            m_flags = flags;
            m_parent = parent;
            m_rootDir = rootDir.ToDir();

            if (!IsReadOnly)
                m_tempDir = new TempDir(Path.Combine(RootDir, TempDirName, Path.GetRandomFileName()));

            if (IsExpanded)
                m_expandedDir = Path.Combine(RootDir, ExpandedDirName).ToDir();

            if (!IsExpanded || Parent == null)
                m_compressedDir = Path.Combine(RootDir, CompressedDirName).ToDir();
        }

        private string GetCachePath(string cacheDir, string hash) {
            return Path.Combine(
                cacheDir,
                hash.ToString().Substring(0, 2),
                hash.ToString().Substring(2, 2),
                hash
            );
        }
        private async Task<string> Download(LfxPointer pointer) {

            // download to temp incase of failed partial download 
            var tempPath = Path.Combine(TempDir, Path.GetRandomFileName());
            var sha256 = SHA256.Create();

            // save to temp file
            using (var fileStream = IOFile.OpenWrite(tempPath)) {

                // compute hash while saving file
                using (var shaStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write)) {

                    // initiate download
                    using (var downloadStream = await new WebClient().OpenReadTaskAsync(pointer.Url)) {

                        // save downloaded file to temp file while reporting progress
                        await downloadStream.CopyToAsync(shaStream, onProgress: OnDownloadProgress);
                    }
                }
            }

            // verify hash (unless hash is unknown)
            var hash = LfxHash.Create(sha256.Hash);
            if (!pointer.Hash.IsNull && pointer.Hash != hash)
                throw new Exception($"Downloaded url '{pointer.Url}' hash '{hash}' is different than expected hash '{pointer.Hash}'.");

            // sourcePath -> targetPath
            var targetPath = GetCachePath(CompressedDir, hash);
            IOFile.Move(tempPath, targetPath);

            // result
            return targetPath;
        }
        private async Task<string> Expand(LfxPointer pointer, string compressedPath) {

            // cache path
            var cachePath = GetCachePath(ExpandedDir, Path.GetFileName(compressedPath));

            // copy file or hard link to file
            if (pointer.IsFile) {

                // hard link (compressed file on same partition)
                if (compressedPath.CanHardLinkTo(cachePath))
                    compressedPath.HardLinkTo(cachePath);

                // copy file (compressed file on separate partition)
                else
                    await compressedPath.CopyToAsync(cachePath, onProgress: OnCopyProgress);
            }

            // epxand archive into directory
            else {
                var tempDir = Path.Combine(TempDir, Path.GetTempFileName());

                // zip
                if (pointer.IsZip)
                    await ExpandZip(compressedPath, tempDir);

                // exe
                else if (pointer.IsExe)
                    await ExpandExe(compressedPath, pointer.Args, tempDir);

                // move directory
                Directory.Move(tempDir, cachePath);
            }

            return cachePath;
        }
        private async Task ExpandExe(string exeFilePath, string arguments, string targetDir) {
            var monitor = targetDir.MonitorGrowth(onGrowth: (path, delta) => OnExpansionProgress?.Invoke(delta));
            var cmdStream = await Cmd.ExecuteAsync(exeFilePath, string.Format(arguments, targetDir));
        }
        private async Task ExpandZip(string zipFilePath, string targetDir) {
            var zipFile = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // allow parallel foreach to yield thread
            await Task.Run(() =>

                // unzip entries in parallel
                Parallel.ForEach(zipFile.Entries, entry => {

                    // ignore directories
                    if (Path.GetFileName(entry.FullName).Length == 0)
                        return;

                    // throw if unzipping target is outside of targetDir
                    var targetPath = Path.Combine(targetDir, entry.FullName);
                    if (!targetPath.StartsWith(targetDir))
                        throw new ArgumentException(
                            $"Zip package '{zipFilePath}' entry '{entry.FullName}' is outside package.");

                    // unzip entry
                    using (var targeStream = File.OpenWrite(targetPath)) {
                        using (var sourceStream = entry.Open()) {

                            // report progress while unzipping
                            sourceStream.CopyTo(targeStream, onProgress: OnExpansionProgress);
                        }
                    }
                })
            );
        }
        private async Task<string> LoadValueFromParentAsync(LfxPointer pointer) {

            // try parent
            if (m_parent != null) {
                var parentPath = await m_parent.GetOrLoadValueAsync(pointer);
                if (parentPath != null)
                    return parentPath;
            }

            return await Download(pointer);
        }

        protected sealed override LfxHash GetKey(LfxPointer pointer) => pointer.Hash;
        protected override bool TryLoadValueSync(LfxHash key, out string value) {
            value = Path.Combine(CacheDir, key);
            return IOFile.Exists(value) || IODirectory.Exists(value);
        }
        protected override async Task<string> LoadValueAsync(LfxPointer pointer) {

            // read-only (lan) cache cannot load files
            if (IsReadOnly)
                return null;

            // load file; will always download if no parent cache
            string compressedPath = await LoadValueFromParentAsync(pointer);

            // compressed (bus) cache holds only files
            if (!IsExpanded)
                return compressedPath;

            // expanded (disk) cache decompresses files to directories
            return await Expand(pointer, compressedPath);
        }

        public event Action<long> OnDownloadProgress;
        public event Action<long> OnExpansionProgress;
        public event Action<long> OnCopyProgress;

        public LfxDirectoryCache Parent => m_parent;

        public LfxStoreFlags Flags => m_flags;
        public bool IsReadOnly => (Flags & LfxStoreFlags.ReadOnly) != 0;
        public bool IsExpanded => (Flags & LfxStoreFlags.Expanded) != 0;

        public string RootDir => m_rootDir;
        public string TempDir => m_tempDir?.Path;
        public string CompressedDir => m_compressedDir;
        public string ExpandedDir => m_expandedDir;
        public string CacheDir => IsExpanded ? ExpandedDir : CompressedDir;

        public void Dispose() {
            m_tempDir?.Dispose();
        }
        ~LfxDirectoryCache() {
            Dispose();
        }

        public override string ToString() => CacheDir;
    }
}