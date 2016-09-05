using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Git.Lfs {

    public sealed class LfsFile {
        public static LfsFile Create(string path) => new LfsFile(path);

        private static LfsConfigFile Find(string file) {
            var configPath = FindFileAbove(file, LfsConfigFile.FileName).FirstOrDefault();
            if (configPath == null)
                return null;
            return new LfsConfigFile(configPath);
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

        private readonly string m_path;
        private readonly LfsConfigFile m_config;
        private readonly Uri m_url;
        private readonly string m_hint;
        private readonly LfsPointer m_pointer;

        private LfsFile(string path) {
            m_path = IOPath.GetFullPath(path);
            if (!File.Exists(m_path))
                throw new Exception($"Expected file '{path}' to exist.");

            m_config = Find(m_path);
            if (m_config == null)
                throw new Exception($"Expected '.lfsconfig' file in directory above '{path}'.");

            var relPathUri = m_config.Directory.ToUrl().MakeRelativeUri(m_path.ToUrl());
            var relPath = relPathUri.ToString();

            m_url = Regex.Replace(
                input: relPath,
                pattern: m_config.Regex,
                replacement: m_config.Url
            ).ToUrl();

            var pointer = LfsPointer.Create(File.OpenRead(m_path));

            if (m_config.Type == LfsPointerType.Archive) {

                if (m_config.Hint != null) {
                    m_hint = Regex.Replace(
                        input: relPath, 
                        pattern: m_config.Regex, 
                        replacement: m_config.Hint
                    );
                }

                // publicly hosted in an archive
                m_pointer = pointer.AddArchive(
                    Url, 
                    Hint
                );
            } 
            
            else if (m_config.Type == LfsPointerType.Curl) {

                // publicly hosted
                m_pointer = pointer.AddUrl(Url);
            }
        }

        public string Path => m_path;
        public LfsConfigFile Config => m_config;
        public Uri Url => m_url;
        public string Hint => m_hint;
        public LfsPointer Pointer => m_pointer;
    }
}