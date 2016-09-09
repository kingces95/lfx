using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;

namespace Git {

    public static class Extensions {

        public static bool EqualPath(this string path, string target) {
            return string.Compare(
                Path.GetFullPath(path),
                Path.GetFullPath(target),
                ignoreCase: false
            ) == 0;
        }
        public static string ToDir(this string dir) {
            if (!dir.EndsWith($"{Path.DirectorySeparatorChar}"))
                dir += Path.DirectorySeparatorChar;
            return dir;
        }
        public static string CopyToDir(this string file, string dir = null) {
            if (dir == null)
                dir = Environment.CurrentDirectory;

            var target = IOPath.Combine(
                IOPath.GetFullPath(dir),
                IOPath.GetFileName(file)
            );

            File.Copy(file, target);
            return target;
        }
        public static Uri ToUrl(this string value, UriKind kind = UriKind.Absolute) {
            Uri url;
            if (!Uri.TryCreate(value, kind, out url))
                throw new Exception($"Expected '{value}' to be '{kind}' url.");
            return url;
        }
        public static string ToParentDir(this string dir) {
            dir = dir.ToDir();
            dir = Directory.GetParent(dir).ToString();
            dir = Directory.GetParent(dir).ToString();
            return dir.ToString().ToDir();
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

                foreach (var o in directories ? 
                    dir.GetDirectories(searchPattern).Select(o => o.FullName) :
                    dir.GetFiles(searchPattern).Select(o => o.FullName)) {

                    var result = o;
                    if (directories)
                        result = result.ToDir();
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
    }
}