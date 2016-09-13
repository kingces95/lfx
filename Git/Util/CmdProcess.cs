using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Git {

    public sealed class CmdProcess : IDisposable {
        public static CmdProcess Start(string exeName, string arguments, string workingDir) {
            var exePath = FindFileOnPath(exeName);

            var processStartInfo = new ProcessStartInfo(
                fileName: exePath,
                arguments: arguments
            );

            if (workingDir != null)
                processStartInfo.WorkingDirectory =
                    Path.GetDirectoryName(workingDir);

            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            return new CmdProcess(
                Process.Start(processStartInfo)
            );
        }

        internal static string FindFileOnPath(string fileName) {
            foreach (var dir in Environment.GetEnvironmentVariable("PATH").Split(';')) {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private Task[] m_tasks;
        private MemoryStream m_standardOutput;
        private MemoryStream m_standardError;

        public readonly Process m_process;
        public long m_outputLength;
        public long m_errorLength;
        public int m_exitCode;

        public CmdProcess(Process process) {
            m_process = process;
            m_standardOutput = new MemoryStream();
            m_standardError = new MemoryStream();

            m_tasks = new[] {
                Task.Run(() => {
                    process.StandardOutput.BaseStream.CopyTo(m_standardOutput);
                    m_standardOutput.Position = 0;
                }),
                Task.Run(() => {
                    process.StandardError.BaseStream.CopyTo(m_standardError);
                    m_standardError.Position = 0;
                })
            };
        }

        public Process Process => m_process;
        public ProcessStartInfo StartInfo => m_process.StartInfo;
        public string FileName => StartInfo.FileName;
        public string Arguments => StartInfo.Arguments;
        public int ExitCode => m_exitCode;

        public long StandardOutputLength => m_outputLength;
        public Stream StandardOutput => m_standardOutput;
        public StreamReader Out => new StreamReader(m_standardOutput);

        public long StandardErrorLength => m_errorLength;
        public Stream StandardError => m_standardError;
        public StreamReader Error => new StreamReader(m_standardError);

        public void Dispose() {
            m_process.WaitForExit();
            Task.WaitAll(m_tasks);

            m_exitCode = m_process.ExitCode;
            m_errorLength = m_standardError.Position;
            m_outputLength = m_standardOutput.Length;

            m_process.Close();

            if (StandardErrorLength != 0 && ExitCode != 0) {
                throw new Exception($"{this} =>{Environment.NewLine}{Error.ReadToEnd()}");
            }
        }

        public override string ToString() => $"{FileName} {Arguments}";
    }

    public sealed class Cmd {

        public static StreamReader Execute(
            string exeName,
            string arguments,
            string workingDir = null,
            Stream inputStream = null) {

            using (var cmdProc = CmdProcess.Start(exeName, arguments, workingDir))
                return cmdProc.Out;
        }
    }
}