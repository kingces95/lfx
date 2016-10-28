using System;
using Git.Lfx;
using Util;
using System.IO;

namespace Lfx {

    public sealed class LfxEnv {
        public const string LfxPointerDirName = @".lfx";
        public const string LfxDirName = @"lfx";
        public const string LfxCacheDirName = @"lfx";
        private const string GitDirName = ".git";

        public static readonly string DefaultUserCacheDir = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            LfxCacheDirName
        ).ToDir();

        public static class EnvironmentVariable {
            public const string DiskCacheName = "LFX_CACHE";
            public const string BusCacheName = "LFX_BUS_CACHE";
            public const string LanCacheName = "LFX_LAN_CACHE";

            public static readonly string DiskCache = Environment.GetEnvironmentVariable(DiskCacheName);
            public static readonly string BusCache = Environment.GetEnvironmentVariable(BusCacheName);
            public static readonly string LanCache = Environment.GetEnvironmentVariable(LanCacheName);
        }

        private readonly string m_workingDir;
        private readonly string m_enlistmentDir;
        private readonly string m_contentDir;
        private readonly string m_pointerDir;
        private readonly string m_diskCacheDir;
        private readonly string m_busCacheDir;
        private readonly string m_lanCacheDir;
        private readonly LfxCache m_cache;

        public LfxEnv() {
            m_workingDir = Environment.CurrentDirectory.ToDir();

            m_enlistmentDir = m_workingDir.FindDirectoryAbove(GitDirName).GetParentDir();
            if (m_enlistmentDir != null) {
                m_contentDir = Path.Combine(m_enlistmentDir, LfxDirName).ToDir();
                m_pointerDir = Path.Combine(m_enlistmentDir, LfxPointerDirName).ToDir();
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
                m_busCacheDir = m_lanCacheDir;

            m_cache = LfxCache.CreateCache(m_diskCacheDir, m_busCacheDir, m_lanCacheDir);
            m_cache.OnProgress += OnProgress;
        }

        public Action<LfxProgressType, long> OnProgress;

        public string WorkingDir => m_workingDir;
        public string EnlistmentDir => m_enlistmentDir;
        public string ContentDir => m_contentDir;
        public string PointerDir => m_pointerDir;
        public string DiskCacheDir => m_diskCacheDir;
        public string BusCacheDir => m_busCacheDir;
        public string LanCacheDir => m_lanCacheDir;

        public void ClearCache() => m_cache.Clear();
        public void CleanCache() => m_cache.Clean();
        public string Checkout(LfxPointer pointer) {
            return m_cache.GetOrLoadValueAsync(pointer).Await();
        }
        public LfxPointer Fetch(LfxIdType type, Uri url, string args = null) {

            switch (type) {
                case LfxIdType.Exe:
                    return m_cache.FetchExe(url, args);

                case LfxIdType.Zip:
                    return m_cache.FetchZip(url);

                case LfxIdType.File:
                default:
                    return m_cache.FetchFile(url);
            }
        }
    }
}