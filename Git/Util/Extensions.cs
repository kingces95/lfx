using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using System.Text;

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
        public static string GetDir(this string dir) {
            var dirName = IOPath.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(dirName))
                return @".\";
            return dirName.ToDir();
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
        public static string GetParentDir(this string dir) {
            return Directory.GetParent(dir.GetDir()).ToString().GetDir();
        }
        public static bool IsDir(this string path) {
            return path.EndsWith($"{IOPath.DirectorySeparatorChar}");
        }

        public static Stream PipeTo(
            this Stream stream,
            string exeName,
            string commandLine,
            string workingDir = null,
            Stream inputStream = null) => Cmd.Stream(exeName, commandLine, workingDir, stream);
        public static void CopyTo(this StreamReader reader, StreamWriter target) {
            var buffer = new char[4096];
            while (true) {
                var count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count == 0)
                    break;
                target.Write(buffer, 0, count);
            }
        }
        public static IEnumerable<string> Lines(this StreamReader stream) {
            while (true) {
                var line = stream.ReadLine();
                if (string.IsNullOrEmpty(line))
                    yield break;

                yield return line;
            }
        }
        public static IEnumerable<string> Lines(
            this TextReader stream, string delimiter = null, int maxLength = int.MaxValue) {

            if (string.IsNullOrEmpty(delimiter))
                delimiter = Environment.NewLine;

            var sb = new StringBuilder();
            var delimiterIndex = 0;

            while (true) {

                if (delimiter.Length == delimiterIndex || sb.Length == maxLength) {
                    yield return sb.ToString();
                    sb.Clear();
                    delimiterIndex = 0;
                }

                var current = stream.Read();
                if (current == -1)
                    break;

                var currentChar = (char)current;

                if (delimiter[delimiterIndex] == currentChar) {
                    delimiterIndex++;
                    continue;
                }

                sb.Append(currentChar);
            }

            yield return sb.ToString();
        }

        public static string Get(this Match match, string name) {
            var group = match.Groups[name];
            if (group == null)
                return null;

            var value = group.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            return value;
        }


        public static V GetValueOrDefault<K, V>(this Dictionary<K, V> source, K key) {
            V value;
            if (!source.TryGetValue(key, out value))
                return default(V);
            return value;
        }

        public static string FindFileAbove(
            this string path, string fileName, bool directory = false) {
            return path.FindFilesAbove(fileName, directory).FirstOrDefault();
        }
        public static IEnumerable<string> FindFilesAbove(
            this string path, string searchPattern, bool directories = false) {
            return new DirectoryInfo(IOPath.GetDirectoryName(path)).FindFilesAbove(searchPattern, directories);
        }
        private static IEnumerable<string> FindFilesAbove(
            this DirectoryInfo dir, string searchPattern, bool directories) {

            if (dir == null)
                yield break;

            if (searchPattern == null)
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