using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Git;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Git.Lfx;
using Util;
using System.IO;

namespace Lfx {

    public enum LfxCmdSwitches {
        Quite, Q,
        Exe,
        Zip,
        Clean,
        Clear,
        Force, F,
    }

    public sealed class LfxCmd {
        public const string Exe = "git-lfx.exe";

        private readonly object Lock = new object();
        private readonly string m_commandLine;
        private readonly LfxEnv m_env;

        public static void Execute(string commandLine) => new LfxCmd(commandLine).Execute();

        private LfxCmd(string commandLine) {
            m_commandLine = commandLine;
            m_env = new LfxEnv();
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
        private void LogProgress() {
            long copy = 0;
            long download = 0;
            long expand = 0;

            m_env.OnProgress += (type, progress) => {
                lock (this) {
                    switch (type) {
                        case LfxProgressType.Copy: copy += progress; break;
                        case LfxProgressType.Download: download += progress; break;
                        case LfxProgressType.Expand: expand += progress; break;
                    }

                    Console.Write($"Download: {download.ToFileSize()}, Copy: {copy.ToFileSize()}, Expand: {expand.ToFileSize()}".PadRight(Console.WindowWidth - 1));
                    Console.CursorLeft = 0;
                }
            };
        }
        private void Lfx(string arguments) => Execute(Exe, arguments);
        private void Execute(string exe, string arguments, string workingDir = null) {
            Log($"> {exe} {arguments}");

            using (var cs = new StreamReader(Cmd.Execute(exe, arguments, workingDir))) {
                var result = cs.ReadToEnd().Trim();
                if (!string.IsNullOrEmpty(result))
                    Log(result);
            }
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
        private void FetchOrPull(bool isPull) {

            // parse
            var minArgs = 1;
            if (isPull)
                minArgs++;

            var args = Parse(
                minArgs: minArgs,
                maxArgs: minArgs + 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Quite, LfxCmdSwitches.Q,
                    LfxCmdSwitches.Exe,
                    LfxCmdSwitches.Zip
               )
            );

            // get flags
            var isQuiet = args.IsSet(LfxCmdSwitches.Quite | LfxCmdSwitches.Q);
            var isExe = args.IsSet(LfxCmdSwitches.Exe);
            var isZip = args.IsSet(LfxCmdSwitches.Zip);
            var type = isExe ? LfxIdType.Exe : isZip ? LfxIdType.Zip : LfxIdType.File;

            // get args
            var argsIndex = 0;
            var contentPath = string.Empty;
            if (isPull) {
                contentPath = Path.GetFullPath(args[argsIndex++]);
                if (!contentPath.IsSubDirOf(m_env.ContentDir))
                    throw new ArgumentException(
                        $"Expected path '{contentPath}' to be in/a sub directory of '{m_env.ContentDir}'.");
            }
            var url = new Uri(args[argsIndex++]);
            var exeArgs = isExe ? args[argsIndex++] : null;

            // log progress
            if (!isQuiet)
                LogProgress();

            // fetch!
            var pointer = m_env.Fetch(type, url, exeArgs);

            // dump pointer
            if (!isPull) {
                Log($"{pointer.Value}");
                return;
            }

            // save poitner
            var recursiveDir = m_env.ContentDir.GetRecursiveDir(contentPath);
            var pointerPath = Path.Combine(m_env.PointerDir, recursiveDir, contentPath.GetFileName());
            Directory.CreateDirectory(pointerPath.GetDir());
            File.WriteAllText(pointerPath, pointer.Value);

            // alias content
            var cachePath = m_env.Checkout(pointer);
            cachePath.AliasPath(contentPath);
        }

        public void Help() {
            Log("git-lfx/0.2.0 (GitHub; corclr)");
            Log("git lfx <command> [<args>]");
            Log();
            Log("Env                                Dump environment.");
            Log();
            Log("Checkout                           Sync content in lfx directory using pointers in .lfx directory.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log();
            Log("Pull <path> <url> [<exeCmd>]       Pull content to path in 'lfx' and add corrisponding pointer to '.lfx'.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    --zip                              Url points to zip archive.");
            Log("    --exe                              Url points to self expanding archive. Use '{0}' in <exeCmd> for target directory.");
            Log();
            Log("Fetch <url> [<cmd>] [<exeCmd>]     Fetch content and echo poitner.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    --zip                              Url points to zip archive.");
            Log("    --exe                              Url points to self expanding archive. Use '{0}' in <cmd> for target directory.");
            Log();
            Log("Cache                              Dump cache stats.");
            Log("    --clean                            Delete orphaned temp directories.");
            Log("    --clear                            Delete all caches on this machine.");
            Log("    -f, --force                        Must be specified with clear.");
        }

        public void Env() {
            Log($"Environment Variables:");
            Log($"  {LfxEnv.EnvironmentVariable.DiskCacheName}={LfxEnv.EnvironmentVariable.DiskCache}");
            Log($"  {LfxEnv.EnvironmentVariable.BusCacheName}={LfxEnv.EnvironmentVariable.BusCache}");
            Log($"  {LfxEnv.EnvironmentVariable.LanCacheName}={LfxEnv.EnvironmentVariable.LanCache}");
            Log();

            Log($"Directories:");
            Log($"  {nameof(m_env.WorkingDir)}: {m_env.WorkingDir}");
            Log($"  {nameof(m_env.EnlistmentDir)}: {m_env.EnlistmentDir}");
            Log($"  {nameof(m_env.ContentDir)}: {m_env.ContentDir}");
            Log($"  {nameof(m_env.PointerDir)}: {m_env.PointerDir}");
            Log($"  {nameof(m_env.DiskCacheDir)}: {m_env.DiskCacheDir}");
            Log($"  {nameof(m_env.BusCacheDir)}: {m_env.BusCacheDir}");
            Log($"  {nameof(m_env.LanCacheDir)}: {m_env.LanCacheDir}");
            Log();
        }
        public void Pull() {
            //pull --exe git https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""
            FetchOrPull(isPull: true);
        }
        public void Fetch() {
            //fetch --exe https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""
            FetchOrPull(isPull: false);
        }
        public void Cache() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Clean,
                    LfxCmdSwitches.Clear,
                    LfxCmdSwitches.F, LfxCmdSwitches.Force
               )
            );

            var clean = args.IsSet(LfxCmdSwitches.Clean);
            var clear = args.IsSet(LfxCmdSwitches.Clear);
            var force = args.IsSet(LfxCmdSwitches.F, LfxCmdSwitches.Force);

            if (clear && !force)
                throw new Exception("To --clear you must also specify --force.");

            // clear
            if (clear) {
                m_env.ClearCache();
                return;
            }

            // clean
            if (clean) {
                m_env.CleanCache();
                return;
            }

            // dump
        }
        public void Checkout() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 0,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Quite,
                    LfxCmdSwitches.Q
               )
            );

            //var lfxFiles = GetFiles(args, pointer: true);

            //var quite = args.IsSet(LfxCmdSwitches.Q, LfxCmdSwitches.Quite);

            //var cache = LfxBlobCache.Create();

            //Batch(args, lfxFiles, async file => {
            //    LfxPointer pointer;
            //    if (!LfxPointer.TryLoad(file.Path, out pointer))
            //        return;

            //    var blob = await cache.LoadAsync(pointer);

            //    blob.Save(file.Path);
            //});
        }
    }
}