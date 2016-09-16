using System;
using System.IO;
using System.Diagnostics;

namespace Git.Lfx {
    public sealed class LfxCmd {
        public const string Exe = "git-lfx.exe";
        public static StreamReader Execute(string commandLine) => Cmd.Execute(Exe, commandLine);
    }
}