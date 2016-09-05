using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Git.Lfs {

    public abstract partial class LfsFile {
        internal static LfsFile Create(LfsLoader loader, string path) {
            path = IOPath.GetFullPath(path);
            if (!File.Exists(path))
                throw new Exception($"Expected file '{path}' to exist.");

            var configFilePath = FindConfigFile(path);
            if (configFilePath == null)
                return new LfsSimpleFile(loader, path);

            var configFile = loader.GetConfigFile(configFilePath);
            if (configFile.Type == LfsPointerType.Archive)
                return new LfsArchiveFile(configFile, path);

            return new LfsCurlFile(configFile, path);
        }

        private static string FindConfigFile(string file) {
            var configPath = FindFileAbove(file, LfsConfigFile.FileName).FirstOrDefault();
            if (configPath == null)
                return null;
            return configPath;
        }
        private static IEnumerable<string> FindFileAbove(string file, string targetFileName) {

            return FindFileAbove(
                new DirectoryInfo(IOPath.GetDirectoryName(file)),
                targetFileName
            );
        }
        private static IEnumerable<string> FindFileAbove(DirectoryInfo dir, string targetFileName) {
            if (dir == null)
                yield break;

            while (dir != null) {
                var target = dir.GetFiles(targetFileName).ToArray();
                if (target.Length == 1)
                    yield return target[0].FullName;
                dir = dir.Parent;
            }
        }

        private readonly LfsLoader m_loader;
        private readonly LfsConfigFile m_configFile;
        private readonly string m_path;

        private LfsFile(LfsLoader loader, LfsConfigFile configFile, string path) {
            m_loader = loader;
            m_configFile = configFile;

            if (m_configFile == null)
                throw new Exception($"Expected '.lfsconfig' file in directory above '{path}'.");
        }

        public string Path => m_path;
        public LfsConfigFile ConfigFile => m_configFile;
        public virtual Uri Url => null;
        public virtual string Hint => null;
        public abstract LfsPointer Pointer { get; }
    }

    public abstract partial class LfsFile {
        private sealed class LfsSimpleFile : LfsFile {
            private readonly LfsPointer m_pointer;

            internal LfsSimpleFile(LfsLoader loader, string path)
                : base(loader, null, path) {

                m_pointer = LfsPointer.Create(path);
            }

            public override LfsPointer Pointer => m_pointer;
        }
        private abstract class LfsWebFile : LfsFile {
            private readonly Uri m_url;
            private readonly Uri m_relPath;

            internal LfsWebFile(LfsConfigFile configFile, string path)
                : base(configFile.Loader, configFile, path) {

                m_relPath = configFile.Directory.ToUrl().MakeRelativeUri(path.ToUrl());

                var url = Regex.Replace(
                    input: m_relPath.ToString(),
                    pattern: configFile.Regex,
                    replacement: configFile.Url
                );

                m_url = url.ToUrl();
            }

            internal Uri RelPath => m_relPath;

            public override Uri Url => m_url;
        }
        private sealed class LfsArchiveFile : LfsWebFile {
            private readonly LfsPointer m_pointer;
            private readonly string m_hint;

            internal LfsArchiveFile(LfsConfigFile configFile, string path)
                : base(configFile, path) {

                if (configFile.Hint != null) {
                    m_hint = Regex.Replace(
                        input: RelPath.ToString(),
                        pattern: configFile.Regex,
                        replacement: configFile.Hint
                    );
                }

                // publicly hosted in an archive
                m_pointer = LfsPointer.Create(path).AddArchive(
                    Url,
                    Hint
                );
            }

            public override LfsPointer Pointer => m_pointer;
            public override string Hint => m_hint;
        }
        private sealed class LfsCurlFile : LfsWebFile {
            private readonly LfsPointer m_pointer;

            internal LfsCurlFile(LfsConfigFile configFile, string path)
                : base(configFile, path) {

                // publicly hosted
                m_pointer = LfsPointer.Create(path).AddUrl(Url);
            }

            public override LfsPointer Pointer => m_pointer;
        }
    }
}