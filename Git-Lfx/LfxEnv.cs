using System;
using Git.Lfx;
using Util;
using System.IO;

namespace Lfx {

    public sealed class LfxEnv {
        public const string LfxPointerDirName = @".lfx";
        public const string LfxDirName = @"lfx";
        private const string GitDirName = ".git";

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
        private readonly LfxCache m_cache;

        public LfxEnv() {
            m_workingDir = Environment.CurrentDirectory.ToDir();

            m_gitDir = m_workingDir.FindDirectoryAbove(GitDirName).GetParentDir();
            if (m_gitDir != null) {
                m_lfxDir = Path.Combine(m_gitDir, LfxDirName).ToDir();
                m_lfxPointerDir = Path.Combine(m_gitDir, LfxPointerDirName).ToDir();
            }

            m_cache = LfxCache.CreateCache(
                diskCacheDir: EnvironmentVariable.DiskCache,
                busCacheDir: EnvironmentVariable.BusCache,
                lanCacheDir: EnvironmentVariable.LanCache,
                workingDir: m_workingDir
            );
        }

        public LfxCache Cache => m_cache;
        public string WorkingDir => m_workingDir;
        public string GitDir => m_gitDir;
        public string LfxDir => m_lfxDir;
        public string LfxPointerDir => m_lfxPointerDir;
    }
}