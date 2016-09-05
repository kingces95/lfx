using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsObjectsCache {
        private readonly LfsObjectsCache m_parent;
        private readonly string m_objectsDir;

        public LfsObjectsCache(
            string objectsDir, 
            LfsObjectsCache parent = null) {

            m_parent = parent;
            m_objectsDir = objectsDir;
            Directory.CreateDirectory(m_objectsDir);
        }

        private LfsHash Add(string file) => Add(File.OpenRead(file));
        private LfsHash Add(Stream stream) {
            if (!stream.CanSeek)
                stream = stream.ToMemoryStream();

            var position = stream.Position;
            var hash = stream.ComputeHash();
            var file = GetPath(hash);
            if (File.Exists(file))
                return hash;

            stream.Position = position;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            stream.CopyTo(File.OpenWrite(file));
            return hash;
        }
        private void Add(LfsObjectsCache cache, LfsHash hash) {
            if (Contains(hash))
                return;

            var path = cache.GetPath(hash);
            if (!File.Exists(path))
                throw new Exception($"Expected '{cache}' to contain '{hash}'.");

            File.Copy(path, GetPath(hash));
        }

        internal string GetPath(LfsHash hash) => Path.Combine(
            m_objectsDir,
            hash.ToString().Substring(0, 2),
            hash.ToString().Substring(2, 2),
            hash
        );

        public string Load(LfsPointer pointer) {
            var path = Get(pointer.Hash);
            if (path == null) {
                if (pointer.Type == LfsPointerType.Archive)
                    LoadArchive(pointer.Url);

                else if (pointer.Type == LfsPointerType.Curl)
                    Load(pointer.Url);
            }

            path = Get(pointer.Hash);
            if (path == null)
                throw new Exception($"Expected a file resolved by LfsPointer to match its hash: '{pointer}'");

            return path;
        }
        public LfsHash Load(Uri url) {
            if (m_parent != null) {
                var hash = m_parent.Load(url);
                Get(hash);
                return hash;
            }

            using (var tempFile = url.DownloadToTempFile())
                return Add(tempFile);
        }
        public IEnumerable<LfsHash> LoadArchive(Uri url) {
            var list = new List<LfsHash>();

            if (m_parent != null) {
                foreach (var hash in m_parent.LoadArchive(url)) {
                    Get(hash);
                    list.Add(hash);
                }
            } 
            
            else {
                using (var tempFiles = url.DownloadAndUnZip()) {
                    foreach (var tempFile in tempFiles)
                        list.Add(Add(tempFile));
                }
            }

            return list;
        }
        public LfsObjectsCache Parent => m_parent;
        public string Get(LfsHash hash) {
            if (!Contains(hash)) {
                var path = m_parent?.Get(hash);
                if (path == null)
                    return null;
                Add(path);
                return path;
            }

            return GetPath(hash);
        }
        public bool Contains(LfsHash hash) => File.Exists(GetPath(hash));
        public int Count => Files().Count();
        public IEnumerable<LfsHash> Files() => 
            Directory.GetFiles(m_objectsDir, "*", SearchOption.AllDirectories)
                .Select(o => LfsHash.Parse(Path.GetFileName(o)));

        public override string ToString() => $"{m_objectsDir}";
    }
}