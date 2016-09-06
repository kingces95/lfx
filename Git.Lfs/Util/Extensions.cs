﻿using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;

namespace Git.Lfs {

    public static class Extensions {
        public static void Download(this Uri uri, string file) {
            new WebClient().DownloadFile(uri, file);
        }
        public static TempFile DownloadToTempFile(this Uri url) {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            url.Download(tempFile);
            return new TempFile(tempFile);
        }
        public static TempDir DownloadAndUnZip(this Uri url) {
            using (var tempFile = DownloadToTempFile(url)) {
                var tempDir = new TempDir();
                ZipFile.ExtractToDirectory(tempFile, tempDir);
                return new TempDir(tempDir);
            }
        }

        public static bool EqualPath(this string path, string target) {
            return string.Compare(
                Path.GetFullPath(path),
                Path.GetFullPath(target),
                ignoreCase: false
            ) == 0;
        }
        public static string ToDirectory(this string dir) {
            if (!dir.EndsWith($"{Path.DirectorySeparatorChar}"))
                dir += Path.DirectorySeparatorChar;
            return dir;
        }
        public static Uri ToUrl(this string value, UriKind kind = UriKind.Absolute) {
            Uri url;
            if (!Uri.TryCreate(value, kind, out url))
                throw new Exception($"Expected '{value}' to be '{kind}' url.");
            return url;
        }

        public static string FindFileAbove(this string path, string fileName, bool directory = false) {
            return path.FindFilesAbove(fileName, directory).FirstOrDefault();
        }
        public static IEnumerable<string> FindFilesAbove(this string path, string searchPattern, bool directories = false) {
            return new DirectoryInfo(IOPath.GetDirectoryName(path)).FindFilesAbove(searchPattern, directories);
        }
        private static IEnumerable<string> FindFilesAbove(
            this DirectoryInfo dir, string searchPattern, bool directories) {

            if (dir == null)
                yield break;

            while (dir != null) {

                foreach (var result in directories ? 
                    dir.GetDirectories(searchPattern).Select(o => o.FullName) :
                    dir.GetFiles(searchPattern).Select(o => o.FullName)) {

                    yield return result;
                }

                dir = dir.Parent;
            }
        }

        public static Stream ToMemoryStream(this Stream stream) {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms;
        }

        public static LfsHash ComputeHash(this string value) => LfsHash.Compute(value);
        public static LfsHash ComputeHash(this Stream stream) => LfsHash.Compute(stream);
        public static LfsHash ComputeHash(this byte[] bytes) => LfsHash.Compute(bytes);
        public static LfsHash ComputeHash(this byte[] bytes, int count) => LfsHash.Compute(bytes, count);
    }
}