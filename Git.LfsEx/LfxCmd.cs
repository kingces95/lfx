using Git.Lfs;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Collections;
using System.Reflection;
using Git;

namespace Lfx {

    public enum LfsCmdSwitches {
        Set,
        Unset,
        List, L,
    }

    public sealed class LfxCmd {
        private readonly GitLoader m_gitLoader;
        private readonly LfsLoader m_loader;
        private readonly string m_commandLine;

        public static void Execute(string commandLine) => new LfxCmd(commandLine).Execute();

        private LfxCmd(string commandLine) {
            m_commandLine = commandLine;
            m_gitLoader = GitLoader.Create();
            m_loader = LfsLoader.Create(m_gitLoader);
        }

        private void Execute() {
            var bf = 
                BindingFlags.Instance| 
                BindingFlags.Public |
                BindingFlags.IgnoreCase;

            try {

                var args = GitCmdArgs.Parse(m_commandLine);
                var name = args.Name;

                // default
                if (args.Name == null) {
                    Default();
                    return;
                }

                // check name
                var method = typeof(LfxCmd).GetMethod(name, bf);
                if (method == null)
                    throw new GitCmdException($"Command '{name}' unrecognized.");

                // dispach
                method.Invoke(null, null);

            } catch (GitCmdException e) {
                Log(e.ToString());
                Help();
            }
        }
        private void Log(string message = null) {
            Console.WriteLine(message);
        }
        private void Lfx(string arguments) {
            new LfxCmd(arguments).Execute();
        }
        private void Git(params string[] arguments) {
            Cmd.Execute("git.exe", arguments);
        }

        public void Help() {
        }
        public void Default() {
            Log($"RootDir={m_gitLoader.EnlistmentDir}");
            Log($"GitDir={m_gitLoader.GitDir}");

            var cache = m_loader.Cache;
            var level = 1;
            while (cache != null) {
                Log($"BlobCache-L{level++}={cache.Store.Directory}");
                cache = cache.Parent;
            }

            Log();
            //GitLog("config --show-origin --get-regex .*lfs.*");
        }
        public void Config() {
            var args = GitCmdArgs.Parse(m_commandLine,
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfsCmdSwitches.List,
                    LfsCmdSwitches.L,
                    LfsCmdSwitches.Unset,
                    LfsCmdSwitches.Set
                )
            );

            if (args.IsSet(LfsCmdSwitches.List, LfsCmdSwitches.L)) {
                Git($"config --show-origin --get-regex .*lfx.*");
            }

            else if (args.IsSet(LfsCmdSwitches.Unset)) {
                Git($"config --unset-all filter.lfx.clean");
                Git($"config --unset-all filter.lfx.smudge");
            }

            else if (args.IsSet(LfsCmdSwitches.Set)) {
                Git($"config --add filter.lfx.clean \"git-lfx clean %f\"");
                Git($"config --add filter.lfx.smudge \"git-lfx smudge %f --\"");
            }

            else throw new GitCmdException("Config argument check failure.");
        }
        public void Init() {
            Git("init");
            Lfx("config --set");
        }
        public void Clean() {
            var args = GitCmdArgs.Parse(m_commandLine);
            var file = m_loader.GetFile(args[1]);
            var pointer = file.Pointer;
            Console.Write(pointer);
        }
        public void Smudge() {
            var args = GitCmdArgs.Parse(m_commandLine);
            var pointerStream = Console.OpenStandardInput();
            if (args.Length == 1)
                pointerStream = File.OpenRead(args[1]);

            var lfsPoitner = LfsPointer.Parse(new StreamReader(pointerStream));
            var lfsObject = m_loader.GetObject(lfsPoitner);
            var contentStream = lfsObject.OpenRead();

            contentStream.CopyTo(Console.OpenStandardOutput());
        }
        public void Clear() {
            var cache = m_loader.Cache;
            while (cache != null) {
                var store = cache.Store;
                Console.WriteLine($"Removing {store.Directory}");
                store.Clear();

                cache = cache.Parent;
            }
        }
    }
}