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

        public static async Task ExpandExe(
            this string exeFilePath, 
            string targetDir,
            string arguments, 
            Action<long> onProgress = null) {

            var monitor = targetDir.MonitorGrowth(onGrowth: (path, delta) => onProgress?.Invoke(delta));
            var cmdStream = await Cmd.ExecuteAsync(exeFilePath, string.Format(arguments, targetDir));
        }
    }
}