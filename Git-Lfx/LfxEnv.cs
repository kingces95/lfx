using System;
using Git.Lfx;
using Util;
using System.IO;

namespace Lfx {

    public sealed class LfxEnv {
        public const string LfxCacheDirName = @"lfx";
        public const string LfxPointerDirName = @".lfx";
        public const string LfxDirName = @"lfx";
        public static readonly string DefaultUserCacheDir = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            LfxCacheDirName
        ).ToDir();

        internal const string GitDirName = ".git";

        public static class EnvironmentVariable {
            public const string DiskCacheName = "LFX_CACHE";
            public const string BusCacheName = "LFX_BUS_CACHE";
            public const string LanCacheName = "LFX_LAN_CACHE";

            public static readonly string DiskCache = Environment.GetEnvironmentVariable(DiskCacheName);
            public static readonly string BusCache = Environment.GetEnvironmentVariable(BusCacheName);
            public static readonly string LanCache = Environment.GetEnvironmentVariable(LanCacheName);
        }

        private readonly string m_workingDir;
        private readonly string m_gitDir;
        private readonly string m_lfxDir;
        private readonly string m_lfxPointerDir;
        private readonly LfxDirectoryCache m_cache;

        public LfxEnv() {

            m_workingDir = Environment.CurrentDirectory.ToDir();
            m_gitDir = m_workingDir.FindDirectoryAbove(GitDirName).GetParentDir();
            if (m_gitDir != null) {
                m_lfxDir = Path.Combine(m_gitDir, LfxDirName).ToDir();
                m_lfxPointerDir = Path.Combine(m_gitDir, LfxPointerDirName).ToDir();
            }

            m_cache = CreateCache();
        }

        private LfxDirectoryCache CreateCache() {

            var diskCacheDir = EnvironmentVariable.DiskCache;
            var busCacheDir = EnvironmentVariable.BusCache;
            var lanCacheDir = EnvironmentVariable.LanCache;

            LfxDirectoryCache cache = null;

            // lan
            if (lanCacheDir != null) {
                cache = new LfxDirectoryCache(
                    lanCacheDir,
                    LfxStoreFlags.ReadOnly
                );
            }

            // bus
            if (busCacheDir != null) {
                cache = new LfxDirectoryCache(
                    busCacheDir,
                    LfxStoreFlags.None,
                    cache
                );
            }

            // default diskCacheDir
            if (diskCacheDir == null) {

                // if working dir partition equals %APPDATA% partition then put cache in %APPDATA%
                var workingDirRoot = Path.GetPathRoot(m_workingDir);
                if (workingDirRoot.EqualsIgnoreCase(Path.GetPathRoot(DefaultUserCacheDir)))
                    diskCacheDir = DefaultUserCacheDir;

                // else put cache at root of working dir
                else
                    diskCacheDir = Path.Combine(workingDirRoot, LfxCacheDirName);
            }

            // disk
            cache = new LfxDirectoryCache(
                diskCacheDir, 
                LfxStoreFlags.Expanded,
                cache
            );

            return cache;
        }

        public LfxDirectoryCache Cache => m_cache;
        public string WorkingDir => m_workingDir;
        public string GitDir => m_gitDir;
        public string LfxDir => m_lfxDir;
        public string LfxPointerDir => m_lfxPointerDir;
    }
}