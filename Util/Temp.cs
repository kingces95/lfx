using System;
using IOPath = System.IO.Path;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Util {

    public class TempCurDir : IDisposable {
        public static implicit operator string(TempCurDir tempDir) => Environment.CurrentDirectory;

        private readonly string m_origCurDir;
        private readonly string m_dir;

        public TempCurDir(string dir) {
            m_origCurDir = Environment.CurrentDirectory.ToDir();
            m_dir = IOPath.Combine(m_origCurDir, dir).ToDir();
            Directory.CreateDirectory(m_dir);
            Environment.CurrentDirectory = m_dir;
        }

        public string Path => m_dir;

        public void Dispose() {
            if (!Directory.Exists(m_origCurDir))
                return;

            Environment.CurrentDirectory = m_origCurDir;
        }

        public override string ToString() => m_dir;
    }
    public class TempDir : IDisposable, IEnumerable<string> {
        public static implicit operator string(TempDir tempDir) => tempDir.ToString();

        private readonly TempCurDir m_tempCurDir;
        private readonly string m_tempDir;

        public TempDir()
            : this(IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName())) {
        }
        public TempDir(string tempDir) {
            Directory.CreateDirectory(tempDir);
            m_tempDir = tempDir.ToDir();
            m_tempCurDir = new TempCurDir(tempDir);
        }

        private void Dispose(bool disposing) {
            m_tempCurDir.Dispose();

            if (disposing)
                GC.SuppressFinalize(this);

            if (!Directory.Exists(m_tempDir))
                return;

            Directory.Delete(m_tempDir, recursive: true);
        }

        public string Path => m_tempDir;

        public IEnumerator<string> GetEnumerator() =>
            Directory.GetFiles(m_tempDir, "*", SearchOption.AllDirectories)
                .Cast<string>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Path;

        public void Dispose() => Dispose(true);
        ~TempDir() { Dispose(false); }
    }
    public class TempFile : IDisposable {
        public static implicit operator string(TempFile tempFile) => tempFile.ToString();

        private readonly string m_path;

        public TempFile() 
            : this(IOPath.GetTempFileName()) {
        }
        public TempFile(string path) {
            m_path = path;
        }

        private void Dispose(bool disposing) {
            if (disposing)
                GC.SuppressFinalize(this);

            if (!File.Exists(m_path))
                return;

            File.Delete(m_path);
        }

        public string Path => m_path;
        public string Directory => Path.GetDir();

        public override string ToString() => Path;
        public void Dispose()  => Dispose(true); 
        ~TempFile() { Dispose(false); }
    }
}