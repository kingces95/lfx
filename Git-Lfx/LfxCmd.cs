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
        private Action<LfxProgressType, long> LogProgress() {
            long copy = 0;
            long download = 0;
            long expand = 0;
            return (type, progress) => {
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

        public void Help() {
            Log("git-lfx/0.2.0 (GitHub; corclr)");
            Log("git lfx <command> [<args>]");
            Log();
            Log("Env                            Dump environment.");
            Log();
            Log("Get <url> [<cmd>] [<file>]     Download content into lfx directory (or echo pointer).");
            Log("    -q, --quite                    Suppress progress reporting.");
            Log("    --zip                          Url points to zip archive.");
            Log("    --exe                          Url points to self expanding archive. Use '{0}' in <cmd> for target directory.");
            Log();
            Log("Sync                           Sync content in lfx directory with pointers in .lfx directory.");
            Log("    -q, --quite                    Suppress progress reporting.");
        }

        public void Env() {
            var env = new LfxEnv();
            Log($"Environment Variables:");
            Log($"  {LfxEnv.EnvironmentVariable.DiskCacheName}={LfxEnv.EnvironmentVariable.DiskCache}");
            Log($"  {LfxEnv.EnvironmentVariable.BusCacheName}={LfxEnv.EnvironmentVariable.BusCache}");
            Log($"  {LfxEnv.EnvironmentVariable.LanCacheName}={LfxEnv.EnvironmentVariable.LanCache}");
            Log();

            Log($"Directories:");
            Log($"  {nameof(env.WorkingDir)}: {env.WorkingDir}");
            Log($"  {nameof(env.EnlistmentDir)}: {env.EnlistmentDir}");
            Log($"  {nameof(env.ContentDir)}: {env.ContentDir}");
            Log($"  {nameof(env.PointerDir)}: {env.PointerDir}");
            Log($"  {nameof(env.DiskCacheDir)}: {env.DiskCacheDir}");
            Log($"  {nameof(env.BusCacheDir)}: {env.BusCacheDir}");
            Log($"  {nameof(env.LanCacheDir)}: {env.LanCacheDir}");
            Log();
        }
        public void Get() {
            //get --exe https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""

            var args = Parse(
                minArgs: 1,
                maxArgs: 2,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Quite, LfxCmdSwitches.Q,
                    LfxCmdSwitches.Exe,
                    LfxCmdSwitches.Zip
               )
            );

            // get args
            var isQuiet = args.IsSet(LfxCmdSwitches.Quite | LfxCmdSwitches.Q);
            var isExe = args.IsSet(LfxCmdSwitches.Exe);
            var isZip = args.IsSet(LfxCmdSwitches.Zip);
            var url = new Uri(args[0]);

            // init env
            var env = new LfxEnv();
            var cache = env.Cache;

            // log progress
            cache.OnProgress += LogProgress();

            // fetch pointer
            LfxPointer pointer;
            if (isZip)
                pointer = cache.FetchZip(url);
            else if (isExe)
                pointer = cache.FetchExe(url, args[1]);
            else
                pointer = cache.FetchFile(url);

            // dump pointer
            Log($"{pointer.Value}");
        }
        public void Sync() {
            //var args = Parse(
            //    minArgs: 0,
            //    maxArgs: 1,
            //    switchInfo: GitCmdSwitchInfo.Create(
            //        LfxCmdSwitches.Quite,
            //        LfxCmdSwitches.Q
            //   )
            //);

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