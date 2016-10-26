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
            Log("Env                    Dump environment.");
            Log();
            Log("Fetch <url>            Download and cache the file referencd by the url.");
            Log();
            Log("Sync                   Synchronize dls directory content pointed to in .dls directory.");
            Log("    -q, --quite            Suppress progress reporting.");
        }

        public void Env() {
            var env = new LfxEnv();
            Log($"Environment Variables:");
            Log($"  {LfxEnv.EnvironmentVariable.DiskCacheName}={LfxEnv.EnvironmentVariable.DiskCache}");
            Log($"  {LfxEnv.EnvironmentVariable.BusCacheName}={LfxEnv.EnvironmentVariable.BusCache}");
            Log($"  {LfxEnv.EnvironmentVariable.LanCacheName}={LfxEnv.EnvironmentVariable.LanCache}");
            Log();

            Log($"Directories:");
            Log($"  {nameof(env.GitDir)}: {env.GitDir}");
            Log($"  {nameof(env.LfxDir)}: {env.LfxDir}");
            Log($"  {nameof(env.LfxPointerDir)}: {env.LfxPointerDir}");
            Log();

            Log($"Cache:");
            var cache = env.Cache;
            while (cache != null) {
                Log($"  {nameof(cache.RootDir)}: {cache.RootDir}");
                Log($"    {nameof(cache.IsExpanded)}={cache.IsExpanded}");
                Log($"    {nameof(cache.IsReadOnly)}={cache.IsReadOnly}");
                Log($"    {nameof(cache.CacheDir)}: {cache.CacheDir}");
                Log($"    {nameof(cache.TempDir)}: {cache.TempDir}");
                if (cache.IsExpanded)
                    Log($"    {nameof(cache.ExpandedDir)}: {cache.ExpandedDir}");
                if (!cache.IsExpanded || cache.Parent == null)
                    Log($"    {nameof(cache.CompressedDir)}: {cache.CompressedDir}");
                Log();

                cache = cache.Parent;
            }
        }
        public void Fetch() {
            // fetch https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe

            var args = Parse(
                minArgs: 1,
                maxArgs: 1
            );

            using (var fTempDir = new TempDir(@"f:\lfx\")) {
                using (var cTempDir = new TempDir(@"c:\lfx\")) {
                    //var packedCache = new LfxBusStore(@"c:\lfx\packed\", @"c:\lfx\packed\temp\");
                    //var unpackedCache = new LfxDiskStore(packedCache, @"f:\lfx\unpacked\", @"f:\lfx\unpacked\temp\");

                    //long total = 0;
                    //packedCache.OnGrowth += l => {
                    //    total += l;
                    //    Console.Write($"L1: {total}");
                    //    Console.CursorLeft = 0;
                    //};
                    //unpackedCache.OnGrowth += l => {
                    //    total += l;
                    //    Console.Write($"L2: {total}");
                    //    Console.CursorLeft = 0;
                    //};

                    //var resultTask = unpackedCache.GetOrLoadValueAsync(LfxPointer.CreateFile(new Uri(args[0])));
                    //resultTask.Wait();
                }
            }
        }
        public void Checkout() {
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