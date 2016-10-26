using System.IO;
using System.Threading.Tasks;
using Util;

namespace Git {

    public sealed class Cmd {

        public static Stream Open(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            return CmdStream.Open(exeName, arguments, workingDir, inputStream);
        }

        public static Stream Execute(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            return ExecuteAsync(exeName, arguments, workingDir, inputStream).Await();
        }
        public async static Task<Stream> ExecuteAsync(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            using (var cmdStream = Open(exeName, arguments, workingDir, inputStream)) {

                // copy cmd stream to memory stream
                var outputStream = new MemoryStream();
                await cmdStream.CopyToAsync(outputStream);

                // reset memory stream
                outputStream.Position = 0;
                return outputStream;
            }
        }
    }
}