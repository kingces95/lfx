using System;
using IOPath = System.IO.Path;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Git {

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

        public void Dispose() {
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

        public string Path => m_tempDir;

        public void Dispose() {
            try {
                m_tempCurDir.Dispose();
            } 
            finally {
                //Directory.Delete(m_tempDir, recursive: true);

                // Directory.Delete threw "Access Denied" where rmdir worked...
                Cmd.Execute("cmd.exe", $"/c rmdir /s/q {m_tempDir}");
            }
        }

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