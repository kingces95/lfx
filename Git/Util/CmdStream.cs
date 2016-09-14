using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Git {

    public sealed class CmdStream : Stream {
        public const int DefaultWaitTime = 5 * 1000;

        public static Stream Open(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            if (workingDir != null)
                workingDir = workingDir.GetDir();

            var processStartInfo = new ProcessStartInfo(
                fileName: FindFileOnPath(exeName),
                arguments: arguments
            ) {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDir,
            };

            var process = Process.Start(processStartInfo);
            return new CmdStream(process, inputStream);
        }

        internal static string FindFileOnPath(string fileName) {
            foreach (var dir in Environment.GetEnvironmentVariable("PATH").Split(';')) {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private readonly Process m_process;
        private readonly Stream m_standardOutput;
        private readonly Task m_consumeStandardError;
        private readonly Stream m_standardError;
        private long m_position;

        public CmdStream(Process process, Stream inputStream) {
            m_process = process;
            m_standardOutput = process.StandardOutput.BaseStream;
            m_standardError = new MemoryStream();

            m_consumeStandardError = Task.Run(() => 
                process.StandardError.BaseStream.CopyTo(m_standardError)
            );

            if (inputStream != null) {
                Task.Run(() => {
                    var standardInput = process.StandardInput.BaseStream;
                    inputStream.CopyTo(standardInput);
                    standardInput.Close();
                });
            }
        }

        private T ThrowInvalidOperation<T>() {
            throw new InvalidOperationException();
        }
        private void WaitForProcessExit() {
            try {
                // wait for process and task compleation
                if (!m_process.WaitForExit(DefaultWaitTime))
                    throw new Exception($"Timed out waiting for command process to exit: {this}");
                m_consumeStandardError.Wait();

                // check for error
                var exitCode = m_process.ExitCode;
                if (m_standardError.Position != 0 && exitCode != 0) {
                    m_standardError.Position = 0;
                    throw new Exception(
                        $"{this} =>{Environment.NewLine}{new StreamReader(m_standardError).ReadToEnd()}");
                }
            } 
            
            finally {
                m_process.Close();
            }
        }

        // read
        public override bool CanRead => true;
        public override long Position {
            get { return m_position; }
            set { throw new InvalidOperationException(); }
        }
        public override int Read(byte[] buffer, int offset, int count) {
            var consumed = (long)m_standardOutput.Read(buffer, offset, count);
            m_position += consumed;

            if (consumed == 0)
                WaitForProcessExit();

            return (int)consumed;
        }

        // seek boilerplate
        public override bool CanSeek => false;
        public override long Length => ThrowInvalidOperation<long>();

        // write boilerplate
        public override bool CanWrite => false;
        public override long Seek(long offset, SeekOrigin origin) => ThrowInvalidOperation<long>();
        public override void SetLength(long value) => ThrowInvalidOperation<long>();
        public override void Write(byte[] buffer, int offset, int count) => ThrowInvalidOperation<long>();
        public override void Flush() => m_standardOutput.Flush();

        public override string ToString() => $"{m_process.StartInfo.FileName} {m_process.StartInfo.Arguments}";
    }
}