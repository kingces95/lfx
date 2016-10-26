using System.IO;

namespace Git {

    public static class Extensions {

        public static Stream PipeTo(
            this Stream stream,
            string exeName,
            string commandLine,
            string workingDir = null,
            Stream inputStream = null) => Cmd.Open(exeName, commandLine, workingDir, stream);
    }
}