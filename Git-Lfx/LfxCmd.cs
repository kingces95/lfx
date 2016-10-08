using Git.Lfx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Git;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace Lfx {

    public enum LfxCmdSwitches {
        Set,
        Unset,
        List, L,
        Sample,
        Content, Pointer,
        Cached, C,
        Others, O,
        Quite, Q,
        Force, F
    }

    public static class Extensions {
        public static bool EqualsIgnoreCase(this string source, string target) {
            return string.Compare(source, target, true) == 0;
        }
        public static string ExpandOrTrim(this string source, int length) {
            if (source.Length > length)
                return source.Substring(0, length);
            return source.PadRight(length);
        }
    }

    public sealed class LfxCmd {
        public const string Exe = "git-lfx.exe";

        private readonly object Lock = new object();
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

                var ae = e as AggregateException;
                if (ae != null)
                    e = ae.InnerException;

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
        private void Batch<T>(GitCmdArgs args, IEnumerable<T> source, Action<T> action) {
            Batch(args, source, o => {
                action(o);
                return Task.FromResult<object>(null);
            });
        }
        private void Batch<T>(GitCmdArgs args, IEnumerable<T> source, Func<T, Task> action) {
            var quite = args.IsSet(LfxCmdSwitches.Q, LfxCmdSwitches.Quite);

            int count = 0;
            double total = source.Count();
            var top = Console.CursorTop;
            var sw = new Stopwatch();
            sw.Start();

            var block = new ActionBlock<T>(async o => {
                if (!quite) {
                    Interlocked.Increment(ref count);
                    lock (Lock) {
                        Console.SetCursorPosition(0, top);
                        Console.Write($"Progress: {count}/{total}={count / total:P}, t={sw.Elapsed:hh':'mm':'ss'.'ff}, {o}"
                            .ExpandOrTrim(Console.BufferWidth - 1));
                    }
                }

                try {
                    await action(o);
                } catch (Exception e) {
                    Console.WriteLine();
                    Console.WriteLine(e.Message);
                }
            });

            foreach (var o in source)
                block.Post(o);
            block.Complete();
            block.Completion.Wait();

            Console.SetCursorPosition(0, top);
            Console.Write($"Progress: {count}/{total}={count / total:P}, t={sw.Elapsed:hh':'mm':'ss'.'ff}"
                .ExpandOrTrim(Console.BufferWidth - 1));
            Console.WriteLine();
        }
        private LfxFileFlags GetFileFlags(GitCmdArgs args, bool pointer = false, bool content = false) {

            var flags = default(LfxFileFlags);
            if (args.IsSet(LfxCmdSwitches.Cached, LfxCmdSwitches.C))
                flags |= LfxFileFlags.Tracked;

            if (args.IsSet(LfxCmdSwitches.Others, LfxCmdSwitches.O))
                flags |= LfxFileFlags.Untracked;

            if (args.IsSet(LfxCmdSwitches.Content))
                flags |= LfxFileFlags.Content;

            if (args.IsSet(LfxCmdSwitches.Pointer))
                flags |= LfxFileFlags.Pointer;

            if (flags == default(LfxFileFlags))
                flags = LfxFileFlags.Tracked;

            if (pointer)
                flags |= LfxFileFlags.Pointer;

            if (content)
                flags |= LfxFileFlags.Content;

            return flags;
        }
        private IEnumerable<GitFile> GetFiles(GitCmdArgs args, bool pointer = false, bool content = false) {

            string dir = null;
            string filter = null;
            if (args.Length > 0) {
                dir = args[0].GetDir();
                filter = Path.GetFileName(args[0]);
            }

            var flags = GetFileFlags(args, pointer, content);
            var lfxFiles = LfxFile.Load(filter, dir, flags);
            return lfxFiles;
        }

        public void Help() {
            Log("git-lfx/0.2.0 (GitHub; corclr)");
            Log("git lfx <command> [<args>]");
            Log();
            Log("Init                   Initialize a new git repository and set lfx filters.");
            Log("    --sample               Adds sample files.");
            Log();
            Log("Clone <url> <target>   Clone repository, set lfx filters, and download any lfx content.");
            Log();
            Log("Checkout <filter>      Batch replace lfx pointer with content in files.");
            Log("    -l, --list             List lfx pointers that would be restored, but don't actually restore.");
            Log("    -c, --cached           Tracked files (default).");
            Log("    -o, --other            Untracked files.");
            Log("    -q, --quite            Suppress progress reporting.");
            Log();
            Log("Reset <filter>         Batch replace lfx files with lfx pointers.");
            Log("    -l, --list             List lfx files that would be reset, but don't actually reset.");
            Log("    -c, --cached           Tracked files (default).");
            Log("    -o, --other            Untracked files.");
            Log("    -q, --quite            Suppress progress reporting.");
            Log("    -f, --force            Force overwriting read-only and system files. Clear hidden files.");
            Log();
            Log("Files <filter>         List files using lfx filters in directory and subdirectories.");
            Log("    -c, --cached           Tracked files (default).");
            Log("    -o, --other            Untracked files.");
            Log("    --content              Only show files with content.");
            Log("    --poitner              Only show files pointing to content.");
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
            Log("Cache                  Dump cache hashes.");
            Log();
            Log("Clean <path>           Write a lfx pointer to a file's content to standard output.");
            Log();
            Log("Smudge                 Resolve a lfx pointer and write its content to standard output.");
            Log("    <path>                 Resolve a lfx pointer stored in a file.");
            Log("    --                     Resolve a lfx pointer read from standard input.");
        }

        public void Env() {
            ParseNoArgsAndNoSwitches();

            var config = LfxConfig.Load();
            var gitConfig = config.GitConfig;

            Log($"Enlistment={gitConfig.EnlistmentDirectory}");
            Log($"GitDirectory={gitConfig.GitDirectory}");
            Log();

            var cache = LfxBlobCache.Create();
            var level = 1;
            Log("BlobCache:");
            while (cache != null) {
                var store = cache.Store;
                Log($"  L{level++}={store.Directory} (count={store.Count}, size={store.Size.ToString("N0")})");
                cache = cache.Parent;
            }
            Log();

            Log("Config:");
            Log($"  {LfxConfigFile.CleanFilterId}={gitConfig[LfxConfigFile.CleanFilterId]}");
            Log($"  {LfxConfigFile.SmudgeFilterId}={gitConfig[LfxConfigFile.SmudgeFilterId]}");
            Log($"  {LfxConfigFile.TypeId}={gitConfig[LfxConfigFile.TypeId]}");
            Log($"  {LfxConfigFile.UrlId}={gitConfig[LfxConfigFile.UrlId]}");
            Log($"  {LfxConfigFile.PatternId}={gitConfig[LfxConfigFile.PatternId]}");
            Log($"  {LfxConfigFile.HintId}={gitConfig[LfxConfigFile.HintId]}");
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
                    LfxCmdSwitches.List,
                    LfxCmdSwitches.L,
                    LfxCmdSwitches.Unset,
                    LfxCmdSwitches.Set
                )
            );

            if (args.IsSet(LfxCmdSwitches.List, LfxCmdSwitches.L)) {
                Git($"config --show-origin --get-regex filter.lfx.*");
            }

            else if (args.IsSet(LfxCmdSwitches.Unset)) {
                Git($"config --unset-all filter.lfx.clean");
                Git($"config --unset-all filter.lfx.smudge");
            }

            else if (args.IsSet(LfxCmdSwitches.Set)) {
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
                    LfxCmdSwitches.Sample
                )
            );
            Git("init");
            Lfx("config --set");

            if (!args.IsSet(LfxCmdSwitches.Sample))
                return;

            File.WriteAllText(".gitattributes", $"* text=auto");

            using (var dls = new TempCurDir("dls")) {
                File.WriteAllLines(".gitattributes", new[] {
                    $"* filter=lfx diff=lfx merge=lfx -text",
                    $".gitattributes filter= diff= merge= text=auto",
                    $".gitignore filter= diff= merge= text=auto",
                    $"packages.config filter= diff= merge= text=auto",
                    $"*.lfxconfig filter= diff= merge= text eol=lf"
                });

                using (var packages = new TempCurDir("tools")) {
                    Git($"config -f nuget.exe.lfxconfig --add lfx.type curl");
                    Git($"config -f nuget.exe.lfxconfig --add lfx.url {nugetUrl}");

                    File.WriteAllText("NuGet.exe", LfxPointer.Create(
                        hash: LfxHash.Parse(nugetHash),
                        count: nugetCount
                    ).AddUrl(nugetUrl.ToUrl()).ToString());
                }

                using (var packages = new TempCurDir("packages")) {
                    File.WriteAllText(".gitignore", $"*.nupkg");

                    Git($"config -f .lfxconfig --add lfx.type archive");
                    Git($"config -f .lfxconfig --add lfx.url {nugetPkgUrl}");
                    Git($"config -f .lfxconfig --add lfx.pattern {nugetPkgPattern}");
                    Git($"config -f .lfxconfig --add lfx.hint {nugetPkgHint}");

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

            LfxPointer pointer;
            if (!LfxPointer.TryLoad(args[0], out pointer))
                pointer = LfxPointer.Create(path);

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
                pointerStream = File.OpenRead(args[0]);

            using (var sr = new StreamReader(pointerStream)) {
                var pointer = LfxPointer.Parse(sr);

                var cache = LfxBlobCache.Create();
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

            var cache = LfxBlobCache.Create();
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
                    LfxCmdSwitches.Cached,
                    LfxCmdSwitches.C,
                    LfxCmdSwitches.Others,
                    LfxCmdSwitches.O,
                    LfxCmdSwitches.Content,
                    LfxCmdSwitches.Pointer
                )
            );

            foreach (var file in GetFiles(args))
                Log($"{file}");
        }
        public void Checkout() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.List,
                    LfxCmdSwitches.L,
                    LfxCmdSwitches.Cached,
                    LfxCmdSwitches.C,
                    LfxCmdSwitches.Others,
                    LfxCmdSwitches.O,
                    LfxCmdSwitches.Quite,
                    LfxCmdSwitches.Q
               )
            );

            var lfxFiles = GetFiles(args, pointer: true);

            var listOnly = args.IsSet(LfxCmdSwitches.L, LfxCmdSwitches.List);
            if (listOnly) {
                foreach (var file in lfxFiles)
                    Log($"{file}");
                return;
            }

            var cache = LfxBlobCache.Create();

            Batch(args, lfxFiles, async file => {
                LfxPointer pointer;
                if (!LfxPointer.TryLoad(file.Path, out pointer))
                    return;

                var blob = await cache.LoadAsync(pointer);

                blob.Save(file.Path);
            });
        }
        public void Reset() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.List,
                    LfxCmdSwitches.L,
                    LfxCmdSwitches.Cached,
                    LfxCmdSwitches.C,
                    LfxCmdSwitches.Others,
                    LfxCmdSwitches.O,
                    LfxCmdSwitches.Quite,
                    LfxCmdSwitches.Q,
                    LfxCmdSwitches.Force,
                    LfxCmdSwitches.F
                )
            );

            var lfxFiles = GetFiles(args, content: true);

            var listOnly = args.IsSet(LfxCmdSwitches.L, LfxCmdSwitches.List);
            if (listOnly) {
                foreach (var file in lfxFiles)
                    Log($"{file}");
                return;
            }

            var force = args.IsSet(LfxCmdSwitches.F, LfxCmdSwitches.Force);

            var cache = LfxBlobCache.Create();
            Batch(args, lfxFiles, file => {
                var path = file.Path;

                if (force) {
                    var mask = ~(FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System);
                    File.SetAttributes(
                        path: path,
                        fileAttributes: File.GetAttributes(path) & mask
                    );
                }

                if (!LfxPointer.CanLoad(path)) {
                    var blob = cache.Save(path);
                    var pointer = LfxPointer.Create(path, blob.Hash);
                    pointer.Save(path);
                }
            });
        }
        public void Cache() {
            var cache = LfxBlobCache.Create();

            foreach (var blob in cache)
                Console.WriteLine($"{blob.Hash}");
        }
    }
}