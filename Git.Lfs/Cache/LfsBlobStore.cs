using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsBlobStore {
        private readonly string m_objectsDir;

        public LfsBlobStore(
            string objectsDir) {

            m_objectsDir = objectsDir;
            Directory.CreateDirectory(m_objectsDir);
        }

        private string GetPath(LfsHash hash) {
            return Path.Combine(
                m_objectsDir,
                hash.ToString().Substring(0, 2),
                hash.ToString().Substring(2, 2),
                hash
            );
        }
        private LfsBlob Add(Stream stream, LfsHash hash) {

            // insert
            var path = GetPath(hash);
            if (!File.Exists(path)) {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                stream.CopyTo(File.OpenWrite(path));
            }

            return new LfsBlob(this, hash, path);
        }

        public LfsBlob Add(LfsBlob blob) {
            var path = GetPath(blob.Hash);
            if (File.Exists(path))
                return new LfsBlob(this, blob.Hash, path);

            return Add(File.OpenRead(path), blob.Hash);
        }
        public LfsBlob Add(string path) => Add(File.OpenRead(path));
        public LfsBlob Add(Stream stream) {
            if (!stream.CanSeek)
                stream = stream.ToMemoryStream();

            // compute hash
            var position = stream.Position;
            var hash = stream.ComputeHash();

            stream.Position = position;
            return Add(stream, hash);
        }
        public bool TryGet(LfsHash hash, out LfsBlob blob) {
            blob = default(LfsBlob);
            var path = GetPath(hash);
            if (!File.Exists(path))
                return false;
            blob = new LfsBlob(this, hash, path);
            return true;
        }
        public bool Contains(LfsHash hash) {
            LfsBlob blob;
            return TryGet(hash, out blob);
        }
        public int Count => Files().Count();
        public IEnumerable<LfsBlob> Files() {
            return Directory.GetFiles(m_objectsDir, "*", SearchOption.AllDirectories)
                .Select(o => new LfsBlob(this, LfsHash.Parse(Path.GetFileName(o)), o));
        }

        public override string ToString() => $"{m_objectsDir}";
    }
}