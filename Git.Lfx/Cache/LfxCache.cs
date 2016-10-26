using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Util;

namespace Git.Lfx {

    public sealed class LfxCache : IEnumerable<LfxTarget> {
        // disk, bus, lan, wan
        public const string LfxDirName = @"dls";

        public static LfxCache Create(string workingDir = null) {
            if (workingDir == null)
                workingDir = Environment.CurrentDirectory;
            workingDir = workingDir.ToDir();

            var rootDir = Path.GetPathRoot(workingDir);
            var dlsDir = Path.Combine(rootDir, LfxDirName);

            return new LfxCache(dlsDir);
        }

        private readonly ConcurrentDictionary<Uri, Task<LfxTarget>> m_downloads;
        private readonly LfxStore m_store;

        public LfxCache(string storeDir) {
            m_downloads = new ConcurrentDictionary<Uri, Task<LfxTarget>>();
            m_store = new LfxStore(storeDir);
        }

        public LfxStore Store => m_store;

        public LfxTarget Save(string path) => m_store.Add(path.ComputeHash(), path);
        public LfxTarget Load(LfxPointer pointer) {
            return LoadAsync(pointer).Result;
        }
        public LfxTarget LoadFile(Uri url) {
            return LoadFileAsync(url).Result;
        }
        public LfxTarget LoadZip(Uri url) {
            return LoadZipAsync(url).Result;
        }
        public LfxTarget LoadExe(Uri url, string args) {
            return LoadExeAsync(url, args).Result;
        }

        public async Task<LfxTarget> LoadAsync(LfxPointer pointer) {
            LfxTarget blob;
            if (TryGet(pointer.Hash, out blob))
                return blob;

            if (pointer.Type == LfxPointerType.Zip)
                await LoadZipAsync(pointer.Url);

            else if (pointer.Type == LfxPointerType.Exe)
                await LoadExeAsync(pointer.Url, pointer.Args);

            else if (pointer.Type == LfxPointerType.File)
                await LoadFileAsync(pointer.Url);

            if (!TryGet(pointer.Hash, out blob))
                throw new Exception($"Expected LfxPointer not found in downloaded content. Pointer:\n{pointer}");

            return blob;
        }
        public async Task<LfxTarget> LoadFileAsync(Uri url) {
            return await m_downloads.GetOrAdd(
                key: url,
                valueFactory: async o => {
                    using (var tempFile = await url.DownloadToTempFileAsync()) {
                        return m_store.Add(tempFile.Path.ComputeHash(), tempFile);
                    }
                }
            );
        }
        public async Task<LfxTarget> LoadZipAsync(Uri url) {
            return await m_downloads.GetOrAdd(
                key: url, 
                valueFactory: async o => {
                    using (var tempFile = await url.DownloadToTempFileAsync()) {
                        using (var tempDir = new TempDir()) {
                            /*await*/ ZipFile.ExtractToDirectory(tempFile, tempDir);
                            return m_store.Add(tempFile.Path.ComputeHash(), tempDir);
                        }
                    }
                }
            );
        }
        public async Task<LfxTarget> LoadExeAsync(Uri url, string args) {
            return await m_downloads.GetOrAdd(
                key: url,
                valueFactory: async o => {
                    using (var tempFile = await url.DownloadToTempFileAsync(".exe")) {
                        using (var tempDir = new TempDir()) {
                            /*await*/ Cmd.Execute(
                                exeName: tempFile, 
                                arguments: string.Format(args, tempDir), 
                                workingDir: tempFile.Directory
                            );

                            return m_store.Add(tempFile.Path.ComputeHash(), tempDir);
                        }
                    }
                }
            );
        }

        public bool TryGet(LfxHash hash, out LfxTarget blob) {
            return m_store.TryGet(hash, out blob);
        }

        public IEnumerator<LfxTarget> GetEnumerator() {
            return m_store.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}