using System;
using System.Collections.Generic;
using System.IO;
using IODirectory = System.IO.Directory;
using System.Linq;
using System.Collections;

namespace Git.Lfs {

    public sealed class LfsBlobStore : IEnumerable<LfsBlob> {
        private readonly string m_dir;

        public LfsBlobStore(string dir) {
            m_dir = dir.ToDir();
            IODirectory.CreateDirectory(m_dir);
        }

        private string GetPath(LfsHash hash) {
            return Path.Combine(
                m_dir,
                hash.ToString().Substring(0, 2),
                hash.ToString().Substring(2, 2),
                hash
            );
        }
        private LfsBlob Add(Stream stream, LfsHash hash) {

            // insert
            var path = GetPath(hash);
            if (!File.Exists(path)) {
                IODirectory.CreateDirectory(Path.GetDirectoryName(path));
                using (var targetStream = File.OpenWrite(path))
                    stream.CopyTo(targetStream);
            }

            return new LfsBlob(this, hash, path);
        }
        private string[] Files => IODirectory.GetFiles(m_dir, "*", SearchOption.AllDirectories);

        public string Directory => m_dir;
        public LfsBlob Add(LfsBlob blob) {
            var hash = blob.Hash;

            var path = GetPath(hash);
            if (File.Exists(path))
                return new LfsBlob(this, blob.Hash, path);

            using (var stream = File.OpenRead(blob.Path))
                return Add(stream, hash);
        }
        public LfsBlob Add(string path) {
            using (var stream = File.OpenRead(path))
                return Add(stream);
        }
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
        public int Count => Files.Count();
        public long Size => Files.Sum(o => new FileInfo(o).Length);
        public void Clear() => IODirectory.Delete(m_dir, recursive: true);

        public override string ToString() => $"{m_dir}";

        public IEnumerator<LfsBlob> GetEnumerator() {
            return Files
                .Select(o => new LfsBlob(this, LfsHash.Parse(Path.GetFileName(o)), o))
                .GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}