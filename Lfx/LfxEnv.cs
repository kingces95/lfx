using System;
using Util;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Lfx {

    public sealed class LfxEnv {
        public const string LfxInfoDirName = @".lfx";
        public const string LfxDirName = @"lfx";
        public const string LfxCacheDirName = @"lfx";

        public static readonly string DefaultUserCacheDir = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            LfxCacheDirName
        ).ToDir();

        public static class EnvironmentVariable {
            public const string CacheDirName = "LFX_CACHE_DIR";
            public const string ArchiveCacheDirName = "LFX_ARCHIVE_CACHE_DIR";
            public const string ReadOnlyArchiveCacheDirName = "LFX_READONLY_ARCHIVE_CACHE_DIR";

            public static readonly string CacheDir = Environment.GetEnvironmentVariable(CacheDirName);
            public static readonly string ArchiveCacheDir = Environment.GetEnvironmentVariable(ArchiveCacheDirName);
            public static readonly string ReadOnlyArchiveCacheDir = Environment.GetEnvironmentVariable(ReadOnlyArchiveCacheDirName);
        }

        private readonly string m_workingDir;
        private readonly string m_rootDir;
        private readonly string m_aliasesDir;
        private readonly string m_infoDir;
        private readonly string m_cacheDir;
        private readonly string m_archiveCacheDir;
        private readonly string m_readOnlyArchiveCacheDir;
        private readonly LfxLoader m_loader;
        private readonly Lazy<IEnumerable<LfxEnv>> m_subEnv;

        public LfxEnv(string workingDir = null) {
            m_workingDir = (workingDir ?? Environment.CurrentDirectory).ToDir();

            m_infoDir = m_workingDir.FindDirectoryAbove(LfxInfoDirName);
            if (m_infoDir != null) {
                m_rootDir = m_infoDir.GetParentDir();
                m_aliasesDir = Path.Combine(m_rootDir, LfxDirName).ToDir();
                m_infoDir = Path.Combine(m_rootDir, LfxInfoDirName).ToDir();
            }

            m_cacheDir = EnvironmentVariable.CacheDir.ToDir();
            m_archiveCacheDir = EnvironmentVariable.ArchiveCacheDir.ToDir();
            m_readOnlyArchiveCacheDir = EnvironmentVariable.ReadOnlyArchiveCacheDir.ToDir();

            // default diskCacheDir
            if (m_cacheDir == null) {

                // if working dir partition equals %APPDATA% partition then put cache in %APPDATA%
                if (m_workingDir.PathRootEquals(DefaultUserCacheDir))
                    m_cacheDir = DefaultUserCacheDir;

                // else put cache at root of working dir
                else
                    m_cacheDir = Path.Combine(Path.GetPathRoot(m_workingDir), LfxCacheDirName).ToDir();
            }

            // default busCacheDir
            if (m_archiveCacheDir == null)
                m_archiveCacheDir = m_cacheDir;

            m_loader = new LfxLoader(m_cacheDir, m_archiveCacheDir, m_readOnlyArchiveCacheDir);
            m_loader.OnProgress += progress => OnProgress?.Invoke(progress);

            m_subEnv = new Lazy<IEnumerable<LfxEnv>>(() => {
                if (m_rootDir == null)
                    return Enumerable.Empty<LfxEnv>();

                var result =
                    from dir in m_rootDir.GetAllDirectories()
                    where dir.GetDirectoryName().EqualsIgnoreCase(LfxInfoDirName)
                    where !dir.EqualsIgnoreCase(m_infoDir)
                    let subWorkingDir = dir.GetParentDir()
                    select new LfxEnv(subWorkingDir);
                return result.ToList();
            });
        }

        public void ReLog(object obj = null) {
            var value = obj?.ToString() ?? string.Empty;
            value = value.PadRight(Console.WindowWidth - 1);

            lock (this) {
                Console.Write(value);
                Console.CursorLeft = 0;
            }
        }
        public void Log(object obj = null) {
            var value = obj?.ToString() ?? string.Empty;
            value = value.PadRight(Console.WindowWidth - 1);

            lock (this)
                Console.WriteLine(value);
        }

        // compose loader
        public event LfxProgressDelegate OnProgress;
        public Task<LfxContent> GetOrLoadContentAsync(LfxPointer pointer) {
            return m_loader.GetOrLoadContentAsync(pointer);
        }
        public Task<LfxContent> GetOrLoadContentAsync(LfxInfo info) {
            return m_loader.GetOrLoadContentAsync(info);
        }
        public bool TryGetContent(LfxInfo info, out LfxContent content) {
            return m_loader.TryGetContent(info, out content);
        }
        public IEnumerable<LfxInfo> GetInfos(LfxArchiveId hash) {
            return m_loader.GetInfos(hash);
        }
        public bool TryGetArchiveId(Uri url, out LfxArchiveId hash) {
            return m_loader.TryGetArchiveId(url, out hash);
        }
        public IEnumerable<LfxContent> CacheContent() => m_loader.Content();

        // environmental paths
        public string WorkingDir => m_workingDir;
        public string RootDir => m_rootDir;
        public string Dir => m_aliasesDir;
        public string InfoDir => m_infoDir;
        public string CacheDir => m_cacheDir;
        public string ArchiveCacheDir => m_archiveCacheDir;
        public string ReadOnlyArchiveCacheDir => m_readOnlyArchiveCacheDir;

        // sub-environments
        public IEnumerable<LfxEnv> SubEnvironments() => m_subEnv.Value;
        public IEnumerable<LfxPath> LocalPaths() {
            foreach (var path in GetPath().Paths(recurse: true))
                yield return path;
        }
        public IEnumerable<LfxPath> AllPaths() {
            foreach (var path in LocalPaths())
                yield return path;

            foreach (var env in SubEnvironments()) {
                foreach (var subPath in env.LocalPaths())
                    yield return subPath;
            }
        }

        // paths
        public bool TryGetPath(string path, out LfxPath lfxPath) {
            return LfxPath.TryCreate(Dir, InfoDir, path.GetFullPath(), out lfxPath);
        }
        public LfxPath GetPath(string path = null) {
            if (Dir == null)
                throw new InvalidOperationException($"Path '{path}' is not in an lfx environment.");

            return LfxPath.Create(Dir, InfoDir, path);
        }
        internal LfxLoadAction GetLoadAction(LfxInfo info) {
            return m_loader.GetLoadAction(info);
        }
        public IEnumerable<LfxCount> GetLoadEffort(
            IEnumerable<LfxPath> paths,
            out IEnumerable<LfxPath> pathsWithoutMetadata) {
            return m_loader.GetLoadEffort(paths, out pathsWithoutMetadata);
        }

        // housecleaning
        public void ClearCache() {
            CacheDir.DeletePath();
            ArchiveCacheDir.DeletePath();
        }
        public void CleanCache() => m_loader.Clean();

        internal Task GetOrLoadEntryAsync(object pointer) {
            throw new NotImplementedException();
        }

    }
}