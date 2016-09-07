using System;
using System.IO;
using System.Diagnostics;

namespace Git.Lfs {
    public sealed class LfsCmd {
        public const string Exe = "git-lfs.exe";

        public static StreamReader Execute(string[] args) => 
            Cmd.Execute(Exe, args);

        public static StreamReader Execute(string commandLine) => 
            Cmd.Execute(Exe, commandLine);
    }
}