using System;
using Git.Lfx;
using Util;
using System.IO;
using System.Threading.Tasks;

namespace Lfx {

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

        // compose loader
        public event LfxProgressDelegate OnProgress;
        public Task<string> GetOrLoadContentAsync(LfxInfo info) {
            return m_loader.GetOrLoadContentAsync(info);
        }
        public LfxEntry GetOrLoadEntry(LfxPointer pointer) {
            return m_loader.GetOrLoadEntry(pointer);
        }

        // environmental paths
        public string WorkingDir => m_workingDir;
        public string EnlistmentDir => m_enlistmentDir;
        public string ContentDir => m_contentDir;
        public string InfoDir => m_infoDir;
        public string DiskCacheDir => m_diskCacheDir;
        public string BusCacheDir => m_busCacheDir;
        public string LanCacheDir => m_lanCacheDir;

        // housecleaning
        public void ClearCache() {
            DiskCacheDir.DeletePath();
            BusCacheDir.DeletePath();
        }
        public void CleanCache() => m_loader.Clean();
    }
}