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
        public const string Exe = "git-lfx.exe";

        private readonly string m_commandLine;

        public static void Execute(string commandLine) => new LfxCmd(commandLine).Execute();

        private LfxCmd(string commandLine) {
            m_commandLine = commandLine;
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
                    Help();
                    return;
                }

                // check name
                var method = typeof(LfxCmd).GetMethod(name, bf);
                if (method == null)
                    throw new GitCmdException($"Command '{name}' unrecognized.");

                // dispach
                method.Invoke(this, null);

            } catch (TargetInvocationException tie) {
                var e = tie.InnerException;
                Log($"{e.GetType()}: {e.Message}");

            } catch (Exception e) {
                Log($"{e.GetType()}: {e.Message}");
            }
        }
        private GitCmdArgs Parse(
            int minArgs = 0,
            int maxArgs = int.MaxValue,
            IEnumerable<GitCmdSwitchInfo> switchInfo = null) {

            if (switchInfo == null)
                switchInfo = GitCmdSwitchInfo.Create();

            return GitCmdArgs.Parse(m_commandLine, minArgs, maxArgs, switchInfo);
        }
        private void ParseNoArgsAndNoSwitches() {
            Parse(maxArgs: 0);
        }
        private void Log(string message = null) {
            Console.WriteLine(message);
        }
        private void Lfx(string arguments) => Execute(Exe, arguments);
        private void Git(string arguments) => Execute(GitCmd.Exe, arguments);
        private void Execute(string exe, string arguments) {
            Log($"> {exe} {arguments}");
            var result = Cmd.Execute(exe, arguments).ReadToEnd().Trim();
            if (!string.IsNullOrEmpty(result))
                Log(result);
        }

        public void Help() {
            Log("git-lfx/0.0.1 (GitHub; corclr)");
            Log("git lfs <command> [<args>]");
            Log();
            Log("Env                    Display the Git LFX environment.");
            Log();
            Log("Config                 Manage git config files settings.");
            Log("    -l, --list             List lfx filters.");
            Log("    --unset                Unset lfx filters.");
            Log("    --set                  Set lfx filters.");
            Log();
            Log("Init                   Initialize a new git repository; Set lfx filters.");
            Log();
            Log("Clear                  Delete all caches.");
            Log();
            Log("Clone <url> <target>   Clone repository containing lfx content.");
            Log();
            Log("Show <path>            Write contents of a staged file (after running clean filter).");
            Log();
            Log("Clean <path>           Write a lfx pointer to a file's content to standard output.");
            Log();
            Log("Smudge                 Resolve a lfx pointer and write its content to standard output.");
            Log("    <path>                 Resolve a lfx pointer stored in a file.");
            Log("    --                     Resolve a lfx pointer read from standard input.");
        }
        public void Env() {
            ParseNoArgsAndNoSwitches();

            var config = LfsConfig.Load();
            var gitConfig = config.GitConfig;

            Log($"Enlistment={gitConfig.EnlistmentDirectory}");
            Log($"GitDirectory={gitConfig.GitDirectory}");
            Log();

            var cache = LfsBlobCache.Create();
            var level = 1;
            Log("BlobCache:");
            while (cache != null) {
                var store = cache.Store;
                Log($"  L{level++}={store.Directory} (count={store.Count}, size={store.Size.ToString("N0")})");
                cache = cache.Parent;
            }
            Log();

            Log("Config:");
            Log($"  {LfsConfigFile.CleanFilterId}={gitConfig[LfsConfigFile.CleanFilterId]}");
            Log($"  {LfsConfigFile.SmudgeFilterId}={gitConfig[LfsConfigFile.SmudgeFilterId]}");
            Log($"  {LfsConfigFile.TypeId}={gitConfig[LfsConfigFile.TypeId]}");
            Log($"  {LfsConfigFile.UrlId}={gitConfig[LfsConfigFile.UrlId]}");
            Log($"  {LfsConfigFile.PatternId}={gitConfig[LfsConfigFile.PatternId]}");
            Log($"  {LfsConfigFile.ArchiveHintId}={gitConfig[LfsConfigFile.ArchiveHintId]}");
            Log();

            Log("Config Files:");
            var configLevel = config;
            while (configLevel != null) {
                var configFile = configLevel.ConfigFile;
                Log($"  {configFile}");
                foreach (var value in configFile) {
                    Log($"    {value}");
                }
                configLevel = configLevel.Parent;
            }
            Log();
        }
        public void Config() {
            var args = Parse(
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfsCmdSwitches.List,
                    LfsCmdSwitches.L,
                    LfsCmdSwitches.Unset,
                    LfsCmdSwitches.Set
                )
            );

            if (args.IsSet(LfsCmdSwitches.List, LfsCmdSwitches.L)) {
                Git($"config --show-origin --get-regex filter.lfx.*");
            }

            else if (args.IsSet(LfsCmdSwitches.Unset)) {
                Git($"config --unset-all filter.lfx.clean");
                Git($"config --unset-all filter.lfx.smudge");
            }

            else if (args.IsSet(LfsCmdSwitches.Set)) {
                Git($"config --replace-all filter.lfx.clean \"git-lfx clean %f\"");
                Git($"config --replace-all filter.lfx.smudge \"git-lfx smudge --\"");
            }

            else throw new GitCmdException("Config argument check failure.");
        }
        public void Init() {
            ParseNoArgsAndNoSwitches();
            Git("init");
            Lfx("config --set");
        }
        public void Clone() {
            var args = Parse(
                minArgs: 2,
                maxArgs: 2
            );

            var repositoryUrl = args[0];
            var target = args[1];

            Git($"clone {repositoryUrl} {target} -n");
            using (var dir = new TempCurDir(target)) {
                Lfx($"config --set");
                Git($"checkout");
            }
        }
        public void Clean() {
            var args = Parse(
                maxArgs: 1,
                minArgs: 1
            );

            var path = Path.GetFullPath(args[0]);
            if (!File.Exists(path))
                throw new Exception($"Expected file '{path}' to exist.");

            var pointer = LfsPointer.Create(path);
            Console.Write(pointer);
        }
        public void Smudge() {
            var args = Parse(
                maxArgs: 1, 
                minArgs: 0,
                switchInfo: new[] {
                    GitCmdSwitchInfo.Blank
                }
            );

            var pointerStream = Console.OpenStandardInput();
            if (args.Length == 1)
                pointerStream = File.OpenRead(args[1]);

            var lfsPoitner = LfsPointer.Parse(new StreamReader(pointerStream));

            var cache = LfsBlobCache.Create();
            var blob = cache.Load(lfsPoitner);
            var contentStream = blob.OpenRead();

            contentStream.CopyTo(Console.OpenStandardOutput());
        }
        public void Show() {
            var args = Parse(
                maxArgs: 1,
                minArgs: 1
            );

            var path = Path.GetFullPath(args[0]);
            var rootDir = Environment.CurrentDirectory.ToDir();
            var relUrl = rootDir.ToUrl().MakeRelativeUri(path.ToUrl());

            Git($"show :./{relUrl}");
        }
        public void Clear() {
            ParseNoArgsAndNoSwitches();

            var cache = LfsBlobCache.Create();
            while (cache != null) {
                var store = cache.Store;
                Console.WriteLine($"Removing {store.Directory}");
                store.Clear();

                cache = cache.Parent;
            }
        }
    }
}