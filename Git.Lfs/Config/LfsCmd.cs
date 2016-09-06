using System;
using System.IO;
using System.Diagnostics;

namespace Git.Lfs {
    public sealed class LfsCmd {
        public const string Exe = "git-lfs.exe";

        public static StreamReader Execute(string[] args) => 
            Cmd.Execute(Exe, args);

        public static StreamReader Execute(string commandLine) => 
            Cmd.Execute(Exe, commandLine);
    }

    public sealed class Cmd {

        internal static string FindFileOnPath(string fileName) {
            foreach (var dir in Environment.GetEnvironmentVariable("PATH").Split(';')) {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
        public static StreamReader Execute(
            string exeName,
            string[] args) {

            return Execute(exeName, string.Join(" ", args));
        }
        public static StreamReader Execute(
            string exeName, 
            string commandLine, 
            Stream inputStream = null) {

            var exePath = FindFileOnPath(exeName);

            var processStartInfo = new ProcessStartInfo(
                fileName: exePath,
                arguments: commandLine
            );

            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            var process = Process.Start(processStartInfo);

            process.Start();
            process.WaitForExit();

            return process.StandardOutput;
        }
    }
}