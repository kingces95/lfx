using System;
using Git.Lfx;
using Util;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;

namespace Lfx {
    public struct LfxRepoInfo {
        public static implicit operator LfxInfo(LfxRepoInfo info) => info.m_info;

        private readonly LfxInfo m_info;
        private readonly string m_infoPath;
        private readonly string m_contentPath;

        public LfxRepoInfo(
            LfxEnv env,
            string infoPath) {

            m_infoPath = infoPath;
            m_info = LfxInfo.Load(infoPath);

            var recursiveDir = env.InfoDir.GetRecursiveDir(infoPath);
            m_contentPath = env.ContentDir.PathCombine(recursiveDir, infoPath.GetFileName());
        }

        // repo paths
        public string InfoPath => m_infoPath;
        public string ContentPath => m_contentPath;

        // compose info
        public LfxInfo Info => m_info;
        public LfxPointer Pointer => m_info.Pointer;
        public bool HasMetadata => Info.HasMetadata;
        public LfxPointerType Type => Info.Type;
        public bool IsExe => Info.IsExe;
        public bool IsZip => Info.IsZip;
        public bool IsFile => Info.IsFile;
        public int Version => Info.Version;
        public Uri Url => Info.Url;
        public LfxHash Hash => Info.Hash;
        public string Args => Info.Args;
        public long Size => Info.Size;
        public long? ContentSize => Info.ContentSize;

        public override string ToString() => InfoPath;
    }

    public sealed class LfxEnv {
        public const string LfxInfoDirName = @".lfx";
        public const string LfxDirName = @"lfx";
        public const string LfxCacheDirName = @"lfx";
        private const string GitDirName = ".git";

        public static readonly string DefaultUserCacheDir = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            LfxCacheDirName
        ).ToDir();

        public static class EnvironmentVariable {
            public const string DiskCacheName = "LFX_DISK_CACHE_DIR";
            public const string BusCacheName = "LFX_BUS_CACHE_DIR";
            public const string LanCacheName = "LFX_LAN_CACHE_DIR";

            public static readonly string DiskCache = Environment.GetEnvironmentVariable(DiskCacheName);
            public static readonly string BusCache = Environment.GetEnvironmentVariable(BusCacheName);
            public static readonly string LanCache = Environment.GetEnvironmentVariable(LanCacheName);
        }

        private readonly string m_workingDir;
        private readonly string m_enlistmentDir;
        private readonly string m_contentDir;
        private readonly string m_infoDir;
        private readonly string m_diskCacheDir;
        private readonly string m_busCacheDir;
        private readonly string m_lanCacheDir;
        private readonly LfxLoader m_loader;

        public LfxEnv() {
            m_workingDir = Environment.CurrentDirectory.ToDir();

            m_enlistmentDir = m_workingDir.FindDirectoryAbove(GitDirName)?.GetParentDir();
            if (m_enlistmentDir != null) {
                m_contentDir = Path.Combine(m_enlistmentDir, LfxDirName).ToDir();
                m_infoDir = Path.Combine(m_enlistmentDir, LfxInfoDirName).ToDir();
            }

            m_diskCacheDir = EnvironmentVariable.DiskCache.ToDir();
            m_busCacheDir = EnvironmentVariable.BusCache.ToDir();
            m_lanCacheDir = EnvironmentVariable.LanCache.ToDir();

            // default diskCacheDir
            if (m_diskCacheDir == null) {

                // if working dir partition equals %APPDATA% partition then put cache in %APPDATA%
                if (m_workingDir.PathRootEquals(DefaultUserCacheDir))
                    m_diskCacheDir = DefaultUserCacheDir;

                // else put cache at root of working dir
                else
                    m_diskCacheDir = Path.Combine(Path.GetPathRoot(m_workingDir), LfxCacheDirName).ToDir();
            }

            // default busCacheDir
            if (m_busCacheDir == null)
                m_busCacheDir = m_diskCacheDir;

            m_loader = new LfxLoader(m_diskCacheDir, m_busCacheDir, m_lanCacheDir);
            m_loader.OnProgress += progress => OnProgress(progress);
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
            lock(this)
                Console.WriteLine(obj?.ToString());
        }

        // compose loader
        public event LfxProgressDelegate OnProgress;
        public Task<LfxEntry> GetOrLoadEntryAsync(LfxInfo info) {
            if (!info.HasMetadata)
                return GetOrLoadEntryAsync(info.Pointer);

            return GetOrLoadEntryAsync(info.Pointer, info.Hash);
        }
        public Task<LfxEntry> GetOrLoadEntryAsync(LfxPointer pointer, LfxHash? expectedHash = null) {
            return m_loader.GetOrLoadEntryAsync(pointer, expectedHash);
        }

        // environmental paths
        public string WorkingDir => m_workingDir;
        public string EnlistmentDir => m_enlistmentDir;
        public string ContentDir => m_contentDir;
        public string InfoDir => m_infoDir;
        public string DiskCacheDir => m_diskCacheDir;
        public string BusCacheDir => m_busCacheDir;
        public string LanCacheDir => m_lanCacheDir;

        public LfxRepoInfo GetRepoInfo(string repoInfoPath) {
            return new LfxRepoInfo(this, repoInfoPath);
        }

        // housecleaning
        public void ClearCache() {
            DiskCacheDir.DeletePath();
            BusCacheDir.DeletePath();
        }
        public void CleanCache() => m_loader.Clean();

        internal Task GetOrLoadEntryAsync(object pointer) {
            throw new NotImplementedException();
        }
    }
}