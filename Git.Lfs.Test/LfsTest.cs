using Git.Lfs;
using NUnit.Framework;
using System;
using System.IO;

namespace Git.Lfs.Test {

    public class LfsTest
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
