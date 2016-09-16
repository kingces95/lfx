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
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Lfx {

    public enum LfsCmdSwitches {
        Set,
        Unset,
        List, L,
        Sample,
        Cached, C,
        Others, O
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
                Log($"{e.GetType()}: {e}");

            } catch (Exception e) {
                Log($"{e.GetType()}: {e}");
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
        private void Log(object obj) => Log(obj?.ToString());
        private void Log(string message = null) {
            Console.WriteLine(message);
        }
        private void Lfx(string arguments) => Execute(Exe, arguments);
        private void Git(string arguments, string workingDir = null) => Execute(GitCmd.Exe, arguments, workingDir);
        private void Execute(string exe, string arguments, string workingDir = null) {
            Log($"> {exe} {arguments}");
            var result = Cmd.Execute(exe, arguments, workingDir).ReadToEnd().Trim();
            if (!string.IsNullOrEmpty(result))
                Log(result);
        }

        public void Help() {
            Log("git-lfx/0.0.1 (GitHub; corclr)");
            Log("git lfs <command> [<args>]");
            Log();
            Log("Init                   Initialize a new git repository and set lfx filters.");
            Log("    --sample               Adds sample files.");
            Log();
            Log("Clone <url> <target>   Clone repository, set lfx filters, and download any lfx content.");
            Log();
            Log("Checkout <filter>      Replace lfx pointer with content in files using lfx filters.");
            Log("    -l, --list             List lfx pointers that would be restored, but don't actually restore.");
            Log();
            Log("Files <filter>         List files using lfx filters in directory and subdirectories.");
            Log("    -c, --cached           Tracked files (default)");
            Log("    -o, --other            Untracked files");
            Log();
            Log("Show <path>            Write contents of a staged file (e.g. after running lfx clean filter).");
            Log();
            Log("Env                    Display the lfx environment.");
            Log();
            Log("Clear                  Delete all lfx caches.");
            Log();
            Log("Config                 Manage git config file lfx settings.");
            Log("    -l, --list             List lfx filters.");
            Log("    --unset                Unset lfx filters.");
            Log("    --set                  Set lfx filters.");
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
            var nugetUrl = "https://dist.nuget.org/win-x86-commandline/v3.4.4/NuGet.exe";
            var nugetHash = "c12d583dd1b5447ac905a334262e02718f641fca3877d0b6117fe44674072a27";
            var nugetCount = 3957976L;

            var nugetPkgUrl = "http://nuget.org/api/v2/package/${id}/${ver}";
            var nugetPkgPattern = "^((?<id>.*?)[.])(?=\\d)(?<ver>[^/]*)/(?<path>.*)$";
            var nugetPkgHint = "${path}";

            var args = Parse(
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfsCmdSwitches.Sample
                )
            );
            Git("init");
            Lfx("config --set");

            if (!args.IsSet(LfsCmdSwitches.Sample))
                return;

            File.WriteAllText(".gitattributes", $"* text=auto");

            using (var dls = new TempCurDir("dls")) {
                File.WriteAllLines(".gitattributes", new[] {
                    $"* filter=lfx diff=lfx merge=lfx -text",
                    $".gitattributes filter= diff= merge= text=auto",
                    $".gitignore filter= diff= merge= text=auto",
                    $"packages.config filter= diff= merge= text=auto",
                    $"*.lfsconfig filter= diff= merge= text eol=lf"
                });

                using (var packages = new TempCurDir("tools")) {
                    Git($"config -f nuget.exe.lfsconfig --add lfx.type curl");
                    Git($"config -f nuget.exe.lfsconfig --add lfx.url {nugetUrl}");

                    File.WriteAllText("NuGet.exe", LfsPointer.Create(
                        hash: LfsHash.Parse(nugetHash),
                        count: nugetCount
                    ).AddUrl(nugetUrl.ToUrl()).ToString());
                }

                using (var packages = new TempCurDir("packages")) {
                    File.WriteAllText(".gitignore", $"*.nupkg");

                    Git($"config -f .lfsconfig --add lfx.type archive");
                    Git($"config -f .lfsconfig --add lfx.url {nugetPkgUrl}");
                    Git($"config -f .lfsconfig --add lfx.pattern {nugetPkgPattern}");
                    Git($"config -f .lfsconfig --add lfx.archiveHint {nugetPkgHint}");

                    new XElement("packages",
                        new XElement("package",
                            new XAttribute("id", "NUnit"),
                            new XAttribute("version", "2.6.4")
                        )
                    ).Save("packages.config");
                }
            }
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

            LfsPointer pointer;
            if (!LfsPointer.TryLoad(args[0], out pointer))
                pointer = LfsPointer.Create(path);

            Console.Write(pointer);
        }
        public void Smudge() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 1, 
                switchInfo: new[] {
                    GitCmdSwitchInfo.Blank
                }
            );

            var pointerStream = Console.OpenStandardInput();
            if (args.Length == 1)
                pointerStream = File.OpenRead(args[1]);

            using (var sr = new StreamReader(pointerStream)) {
                var pointer = LfsPointer.Parse(sr);

                var cache = LfsBlobCache.Create();
                var blob = cache.Load(pointer);

                using (var contentStream = blob.OpenRead())
                    contentStream.CopyTo(Console.OpenStandardOutput());
            }
        }
        public void Show() {
            var args = Parse(
                maxArgs: 1,
                minArgs: 1
            );

            var line = GitCmd.Execute($"ls-files -s {args[0]}").ReadToEnd();
            if (string.IsNullOrEmpty(line))
                return;

            var stageHash = "stageHash";
            var stageNumber = "stageNumber";

            //"100644 176a458f94e0ea5272ce67c36bf30b6be9caf623 0\t.gitattributes": text: auto
            var pattern = $"^\\d*\\s(?<{stageHash}>[a-z0-9]*)\\s(?<{stageNumber}>\\d+)";
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            var hash = GitHash.Parse(match.Get(stageHash));

            var content = GitCmd.Execute($"cat-file -p {hash}").ReadToEnd();
            Console.Write(content);
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
        public void Files() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfsCmdSwitches.Cached,
                    LfsCmdSwitches.C,
                    LfsCmdSwitches.Others,
                    LfsCmdSwitches.O
                )
            );

            string dir = null;
            string filter = null;
            if (args.Length > 0) {
                dir = args[0].GetDir();
                filter = Path.GetFileName(args[0]);
            }

            var flags = default(GitFileFlags);
            if (args.IsSet(LfsCmdSwitches.Cached, LfsCmdSwitches.C))
                flags |= GitFileFlags.Tracked;
            if (args.IsSet(LfsCmdSwitches.Others, LfsCmdSwitches.O))
                flags |= GitFileFlags.Untracked;
            if (flags == default(GitFileFlags))
                flags = GitFileFlags.Tracked;

            var lfxFiles = GitFile.Load(filter, dir, flags).Where(o => o.IsDefined("filter", "lfx"));
            foreach (var file in lfxFiles)
                Log($"{file}");
        }
        public void Checkout() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfsCmdSwitches.List,
                    LfsCmdSwitches.L
                )
            );

            var listOnly = args.IsSet(LfsCmdSwitches.L, LfsCmdSwitches.List);

            string dir = null;
            string filter = null;
            if (args.Length > 0) {
                dir = args[0].GetDir();
                filter = Path.GetFileName(args[0]);
            }

            var lfxFiles = GitFile.Load(filter, dir, GitFileFlags.All)
                .Where(o => o.IsDefined("filter", "lfx"));

            foreach (var file in lfxFiles) {
                LfsPointer pointer;
                using (var sr = new StreamReader(file.Path)) {
                    var canParse = LfsPointer.TryParse(sr, out pointer);
                    if (!canParse)
                        continue;

                    if (listOnly) {
                        Log($"{file}");
                        continue;
                    }
                }

                var cache = LfsBlobCache.Create();
                var blob = cache.Load(pointer);

                using (var contentStream = blob.OpenRead()) {
                    File.Delete(file.Path);
                    using (var sw = File.OpenWrite(file.Path))
                        contentStream.CopyTo(sw);
                }
            }
        }
    }
}