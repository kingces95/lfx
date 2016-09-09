using System;
using System.Collections.Generic;
using System.IO;
using IODirectory = System.IO.Directory;

namespace Git.Lfs {

    public sealed class LfsLoader {
        public const string LfsDirName = @"lfs";

        public static LfsLoader Create(GitLoader gitLoader) {
            var lfsDir = gitLoader.GitDir + LfsDirName.ToDir();
            var objectsDir = lfsDir + LfsBlobCache.ObjectsDirName.ToDir();
            var globalCache = LfsBlobCache.DefaultUserCacheDir;
            var cache = LfsBlobCache.Create(objectsDir, globalCache);
            var loader = Create(cache);
            return loader;
        }
        public static LfsLoader Create(LfsBlobCache cache = null) {
            return new LfsLoader(cache);
        }

        private readonly LfsBlobCache m_cache;
        private readonly Dictionary<LfsHash, LfsObject> m_objects; 
        private readonly Dictionary<FileInfo, LfsConfigFile> m_configFiles;
        private readonly Dictionary<string, LfsFile> m_files;

        private LfsLoader(LfsBlobCache cache) {
            m_cache = cache;
            m_objects = new Dictionary<LfsHash, LfsObject>();
            m_configFiles = new Dictionary<FileInfo, LfsConfigFile>();
            m_files = new Dictionary<string, LfsFile>(
                StringComparer.InvariantCultureIgnoreCase
            );
        }

        public LfsBlobCache Cache => m_cache;
        public LfsFile GetFile(string path) {
            var fullPath = Path.GetFullPath(path);

            LfsFile result;
            if (!m_files.TryGetValue(fullPath, out result))
                m_files[fullPath] = result = LfsFile.Create(this, path);

            return result;
        }
        public LfsConfigFile GetConfigFile(string path) {
            var file = new FileInfo(path);

            LfsConfigFile result;
            if (!m_configFiles.TryGetValue(file, out result))
                m_configFiles[file] = result = new LfsConfigFile(this, path);

            return result;
        }
        public LfsObject GetObject(LfsPointer pointer) {
            if (m_cache == null)
                throw new InvalidOperationException("No cache available.");

            var hash = pointer.Hash;

            LfsObject result;
            if (!m_objects.TryGetValue(hash, out result))
                m_objects[hash] = result = new LfsObject(this, m_cache.Load(pointer));

            return result;
        }
    }
}