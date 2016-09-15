using System;
using System.IO;
using System.Diagnostics;

namespace Git {
    public static class GitCmd {
        public const string Exe = "git.exe";

        public static Stream Stream(
            string commandLine,
            string workingDirectory = null,
            Stream inputStream = null) {

            return Cmd.Stream(Exe, commandLine, workingDirectory, inputStream);
        }
        public static StreamReader Execute(
            string commandLine,
            string workingDirectory = null,
            Stream inputStream = null) {

            return Cmd.Execute(Exe, commandLine, workingDirectory, inputStream);
        }
    }
}