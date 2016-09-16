using System;
using System.Collections.Generic;
using System.IO;
using IODirectory = System.IO.Directory;
using System.Linq;
using System.Collections;

namespace Git.Lfx {

    public sealed class LfxBlobStore : IEnumerable<LfxBlob> {
        private readonly string m_dir;

        public LfxBlobStore(string dir) {
            m_dir = dir.ToDir();
            IODirectory.CreateDirectory(m_dir);
        }

        private string GetPath(LfxHash hash) {
            return Path.Combine(
                m_dir,
                hash.ToString().Substring(0, 2),
                hash.ToString().Substring(2, 2),
                hash
            );
        }
        private LfxBlob Add(Stream stream, LfxHash hash) {

            // insert
            var path = GetPath(hash);
            if (!File.Exists(path)) {
                IODirectory.CreateDirectory(Path.GetDirectoryName(path));
                using (var targetStream = File.OpenWrite(path))
                    stream.CopyTo(targetStream);
            }

            return new LfxBlob(this, hash, path);
        }
        private string[] Files => IODirectory.GetFiles(m_dir, "*", SearchOption.AllDirectories);

        public string Directory => m_dir;
        public LfxBlob Add(LfxBlob blob) {
            var hash = blob.Hash;

            var path = GetPath(hash);
            if (File.Exists(path))
                return new LfxBlob(this, blob.Hash, path);

            using (var stream = File.OpenRead(blob.Path))
                return Add(stream, hash);
        }
        public LfxBlob Add(string path) {
            using (var stream = File.OpenRead(path))
                return Add(stream);
        }
        public LfxBlob Add(Stream stream) {
            if (!stream.CanSeek)
                stream = stream.ToMemoryStream();

            // compute hash
            var position = stream.Position;
            var hash = stream.ComputeHash();

            stream.Position = position;
            return Add(stream, hash);
        }
        public bool TryGet(LfxHash hash, out LfxBlob blob) {
            blob = default(LfxBlob);
            var path = GetPath(hash);
            if (!File.Exists(path))
                return false;
            blob = new LfxBlob(this, hash, path);
            return true;
        }
        public bool Contains(LfxHash hash) {
            LfxBlob blob;
            return TryGet(hash, out blob);
        }
        public int Count => Files.Count();
        public long Size => Files.Sum(o => new FileInfo(o).Length);
        public void Clear() => IODirectory.Delete(m_dir, recursive: true);

        public override string ToString() => $"{m_dir}";

        public IEnumerator<LfxBlob> GetEnumerator() {
            return Files
                .Select(o => new LfxBlob(this, LfxHash.Parse(Path.GetFileName(o)), o))
                .GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}