using System;
using System.IO;
using System.Threading.Tasks;
using Util;

namespace Git {

    public static class Extensions {

        public static Stream PipeTo(
            this Stream stream,
            string exeName,
            string commandLine,
            string workingDir = null,
            Stream inputStream = null) {

            return Cmd.Open(exeName, commandLine, workingDir, stream);
        }

        public static async Task<string> ExpandExe(
            this string exeFilePath, 
            string targetDir,
            string arguments, 
            Action<long> onProgress = null) {

            Directory.CreateDirectory(targetDir);
            using (var monitor = targetDir.MonitorGrowth(onGrowth: (path, delta) => onProgress?.Invoke(delta))) {
                var backSlash = '\\';
                var escapedTargetDir = targetDir.Replace($"{backSlash}", $"{backSlash}{backSlash}");

                var expandedArguments = string.Format(arguments, escapedTargetDir);
                var cmdStream = await Cmd.ExecuteAsync(exeFilePath, expandedArguments);
            }

            return targetDir;
        }
    }
}