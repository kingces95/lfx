using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Git {

    public sealed class Cmd {

        public static Stream Stream(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            return Execute(exeName, arguments, workingDir, inputStream, stream: true);
        }

        public static StreamReader Execute(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            return new StreamReader(
                Execute(exeName, arguments, workingDir, inputStream, stream: false)
            );
        }

        public static Stream Execute(
            string exeName,
            string arguments,
            string workingDir,
            Stream inputStream,
            bool stream) {

            var cmdStream = CmdStream.Open(exeName, arguments, workingDir, inputStream);

            if (!stream) {
                var outputStream = new MemoryStream();
                cmdStream.CopyTo(outputStream);
                outputStream.Position = 0;
                cmdStream = outputStream;
            }

            return cmdStream;
        }
    }
}