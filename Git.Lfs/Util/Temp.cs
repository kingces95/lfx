using System;
using IOPath = System.IO.Path;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Git.Lfs {

    public class TempDir : IDisposable, IEnumerable<string> {
        public static implicit operator string(TempDir tempDir) => tempDir.ToString();

        private readonly string m_tempDir;

        public TempDir()
            : this(IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName())) {
        }
        public TempDir(string tempDir) {
            Directory.CreateDirectory(tempDir);
            m_tempDir = tempDir;
            if (!m_tempDir.EndsWith($"{IOPath.DirectorySeparatorChar}"))
                m_tempDir += IOPath.DirectorySeparatorChar;
        }

        public string Path => m_tempDir;

        public void Dispose() => Directory.Delete(m_tempDir, recursive: true);

        public IEnumerator<string> GetEnumerator() =>
            Directory.GetFiles(m_tempDir, "*", SearchOption.AllDirectories)
                .Cast<string>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Path;
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

        public string Path => m_path;
        public void Dispose() => File.Delete(m_path);

        public override string ToString() => Path;
    }
}