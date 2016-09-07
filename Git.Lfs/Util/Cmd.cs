using System;
using System.IO;
using System.Diagnostics;

namespace Git.Lfs {

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
            string[] args,
            string workingDirectory = null) {

            return Execute(exeName, string.Join(" ", args), workingDirectory);
        }
        public static StreamReader Execute(
            string exeName, 
            string commandLine,
            string workingDirectory = null,
            Stream inputStream = null) {

            var exePath = FindFileOnPath(exeName);

            var processStartInfo = new ProcessStartInfo(
                fileName: exePath,
                arguments: commandLine
            );

            if (workingDirectory != null)
                processStartInfo.WorkingDirectory = workingDirectory;

            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            var process = Process.Start(processStartInfo);
            process.WaitForExit();

            var error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error))
                ;// throw new Exception($"{exeName} {commandLine} > {error}");
            process.StandardError.Close();

            var ms = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(ms);
            process.Close();

            ms.Position = 0;
            return new StreamReader(ms);
        }
    }
}