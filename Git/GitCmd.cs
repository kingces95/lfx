using System;
using System.IO;
using System.Diagnostics;

namespace Git {
    public sealed class GitCmd {
        public const string Exe = "git.exe";

        public static StreamReader Execute(
            string commandLine,
            string workingDirectory = null,
            Stream inputStream = null) {

            return Cmd.Execute(Exe, commandLine, workingDirectory, inputStream);
        }
    }
}