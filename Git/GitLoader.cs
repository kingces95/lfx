using System;

namespace Git {

    public sealed class GitLoader {
        public const string GitDirName = ".git";

        public static string EnlistmentDirectory => GetEnlistmentDirectory();
        public static string GetEnlistmentDirectory(string workingDir = null) {
            workingDir = workingDir ?? Environment.CurrentDirectory.ToDir();
            var gitDir = workingDir.FindFileAbove(GitDirName, directory: true);
            if (gitDir == null)
                return null;
            return gitDir.ToParentDir();
        }
        public static GitLoader Create(string workingDir = null) {
            return new GitLoader(GetEnlistmentDirectory(workingDir));
        }

        private readonly string m_enlistmentDir;

        private GitLoader(string enlistmentDir) {
            m_enlistmentDir = enlistmentDir;
        }

        public string GitDir => m_enlistmentDir + GitDirName.ToDir();
        public string EnlistmentDir => m_enlistmentDir;
    }
}