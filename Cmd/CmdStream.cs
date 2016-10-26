using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Util;
using System.Threading;

namespace Git {

    public sealed class CmdStream : Stream {

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

            return Open(processStartInfo, inputStream);
        }
        public static Stream Open(
            ProcessStartInfo processStartInfo,
            Stream inputStream = null) {

            return new CmdStream(processStartInfo, inputStream);
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
        private readonly int m_id;
        private readonly Stream m_standardOutput;
        private readonly Task m_consumeStandardError;
        private readonly Stream m_standardError;
        private readonly Lazy<Task> m_exitedAsync;
        private long m_position;

        private CmdStream(ProcessStartInfo processStartInfo, Stream inputStream) {
            m_process = Process.Start(processStartInfo);
            m_id = m_process.Id;
            m_standardOutput = m_process.StandardOutput.BaseStream;
            m_standardError = new MemoryStream();
            m_exitedAsync = new Lazy<Task>(() => {
                var are = new AutoResetEvent(false);
                m_process.Exited += delegate { are.Set(); };
                if (m_process.HasExited)
                    return Task.FromResult(true);
                return are.WaitOneAsync();
            });

            // todo: async copy of stderr instead of burning threadpool thread
            m_consumeStandardError = Task.Run(() =>
                m_process.StandardError.BaseStream.CopyTo(m_standardError)
            );

            if (inputStream != null) {
                Task.Run(() => {
                    var standardInput = m_process.StandardInput.BaseStream;
                    //new StreamReader(inputStream).CopyTo(new StreamWriter(standardInput));
                    inputStream.CopyTo(standardInput);
                    standardInput.Flush();
                    standardInput.Close();
                });
            }
        }

        private T ThrowInvalidOperation<T>() {
            throw new InvalidOperationException();
        }
        private async Task WaitForProcessExitAsync() {

            // await process exit
            await m_exitedAsync.Value;

            // await consumption of last bits in error stream
            await m_consumeStandardError;

            // check for error
            var exitCode = m_process.ExitCode;
            if (m_standardError.Position != 0 && exitCode != 0) {
                m_standardError.Position = 0;
                var message = $"Command '{this}' exited with '{exitCode}':" + Environment.NewLine +
                    $"{new StreamReader(m_standardError).ReadToEnd()}";
                throw new Exception(message);
            }
        }

        // read
        public override bool CanRead => true;
        public override long Position {
            get { return m_position; }
            set { throw new InvalidOperationException(); }
        }
        public override int Read(byte[] buffer, int offset, int count) {
            return ReadAsync(buffer, offset, count).Await();
        }
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
            return ReadAsync(buffer, offset, count).ToApm(callback, state);
        }
        public override int EndRead(IAsyncResult asyncResult) {
            return ((Task<int>)asyncResult).Result;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            var consumed = await m_standardOutput.ReadAsync(buffer, offset, count);
            m_position += consumed;

            if (consumed == 0)
                await WaitForProcessExitAsync();

            return consumed;
        }

        // seek boilerplate
        public override bool CanSeek => false;
        public override long Length => ThrowInvalidOperation<long>();
        public override long Seek(long offset, SeekOrigin origin) => ThrowInvalidOperation<long>();
        public override void SetLength(long value) => ThrowInvalidOperation<long>();

        // write boilerplate
        public override bool CanWrite => false;
        public override void Write(byte[] buffer, int offset, int count) => ThrowInvalidOperation<long>();
        public override void Flush() => m_standardOutput.Flush();

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (disposing)
                GC.SuppressFinalize(this);
            m_process.Close();
        }
        ~CmdStream() {
            Dispose(false);
        }

        public override string ToString() => $"{m_process.StartInfo.FileName} {m_process.StartInfo.Arguments}";

    }
}