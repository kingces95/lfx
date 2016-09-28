using Git.Lfx;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfx.Test {

    public static class Extensions {
        public static void CopyDir(this string sourceDirName, string destDirName, bool copySubDirs = true) {

            // Get the subdirectories for the specified directory.
            var dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists) {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            var dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
                Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            var files = dir.GetFiles();
            foreach (var file in files) {
                var path = Path.Combine(destDirName, file.Name);
                file.CopyTo(path, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs) {
                foreach (var subdir in dirs) {
                    var path = Path.Combine(destDirName, subdir.Name);
                    CopyDir(subdir.FullName, path, copySubDirs);
                }
            }
        }
    }

    public class LfxTest
    {
        public static readonly string Nl = Environment.NewLine;
        public static readonly string Tab = "\t";

        public static string Mkdir(params string[] segments) {
            var dir = Path.Combine(segments);
            Directory.CreateDirectory(dir);
            return dir;
        }
        public static void Cmd(string exe, string arguments) {
            Console.WriteLine($"{Path.GetFullPath(Environment.CurrentDirectory)}> {exe} {arguments}");
            var sr = global::Git.Cmd.Execute(exe, arguments);
            Console.WriteLine(sr.ReadToEnd());
        }
        public static void Git(string arguments) => GitCmd.Execute(arguments);
        public static void Nuget(string arguments) => Cmd("nuget.exe", arguments);
    }
}
