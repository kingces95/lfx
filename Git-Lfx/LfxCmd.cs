using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Git;
using System.Threading.Tasks;
using Git.Lfx;
using Util;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

namespace Lfx {

    public enum LfxCmdSwitches {
        Quite, Q,
        Exe, Zip, Nuget,
        Clean,
        Clear,
        Force, F,
        Verbose, V,
        Serial,
        Local,
        Hash,
        Disk, Bus, Lan,
        R, Alias
    }

    public sealed class LfxCmdException : Exception {
        public LfxCmdException(string message) : base(message) { }
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
                var result = method.Invoke(this, null) as Task;
                if (result != null)
                    result.Wait();

            } catch (Exception e) {
                Log(e);
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
        private void Log(object value = null) => m_env.Log(value);
        private void Log(Exception e, string indent = "") {
            if (e == null)
                return;

            while (e is TargetInvocationException)
                e = e.InnerException;

            var ae = e as AggregateException;
            if (ae != null) {
                foreach (var aei in ae.InnerExceptions)
                    Log(aei);
                return;
            }

            if (e is LfxCmdException) {
                Log(e.Message);
                return;
            }

            Log($"{indent}{e.GetType()}: {e.Message}");
            Log(e.InnerException, indent + "  ");
        }
        private LfxProgressTracker LogProgress(
            IEnumerable<LfxCount> counts = null, 
            bool verbose = false) {

            if (counts == null)
                counts = Enumerable.Empty<LfxCount>();
            var progress = new LfxProgressTracker(m_env, counts, verbose);
            m_env.OnProgress += progress.UpdateProgress;
            return progress;
        }

        public void Help() {
            Log("git-lfx/0.2.0 (GitHub; corclr)");
            Log("git lfx <command> [<args>]");
            Log();
            Log("Env                                Dump environment.");
            Log();
            Log("Checkout [<path>]                  Sync content in lfx directory using pointers in .lfx directory.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    -v, --verbose                      Log diagnostic information.");
            Log("    --serial                           Download, copy, and/or expand each pointer serially.");
            Log("    --local                            Do not checkout submodules.");
            Log();
            Log("Pull <path> <url> [<exeCmd>]       Pull content to path in 'lfx' and add corrisponding pointer to '.lfx'.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    --zip                              Url points to zip archive.");
            Log("    --exe                              Url points to self expanding archive. Use '{0}' in <exeCmd> for target directory.");
            Log("    --nuget                            Url points to nuget package.");
            Log();
            Log("Fetch <url> [<exeCmd>]             Fetch content and echo pointer.");
            Log("    -q, --quite                        Suppress progress reporting.");
            Log("    --zip                              Url points to zip archive.");
            Log("    --exe                              Url points to self expanding archive. Use '{0}' in <exeCmd> for target directory.");
            Log("    --nuget                            Url points to nuget package.");
            Log();
            Log("Ls [<path>]                        Lists state of aliases in working directory.");
            Log("    -r                                 Recursive.");
            Log("    --alias                            Show alias; Show hard link or junction into cache.");
            Log();
            Log("Show <url|path>                    Show cached pointer for url.");
            Log("    --hash                             Just show the hash for the url.");
            Log();
            Log("Cache                              Dump cache stats.");
            Log("    --clean                            Delete orphaned temp directories.");
            Log("    --clear                            Delete all caches on this machine.");
            Log("    -f, --force                        Must be specified with clear.");
        }
        public void Env() {
            Log($"Environment Variables:");
            Log($"  {LfxEnv.EnvironmentVariable.CacheDirName}={LfxEnv.EnvironmentVariable.CacheDir}");
            Log($"  {LfxEnv.EnvironmentVariable.ArchiveCacheDirName}={LfxEnv.EnvironmentVariable.ArchiveCacheDir}");
            Log($"  {LfxEnv.EnvironmentVariable.ReadOnlyArchiveCacheDirName}={LfxEnv.EnvironmentVariable.ReadOnlyArchiveCacheDir}");
            Log();

            Log($"Directories:");
            Log($"  {nameof(m_env.Dir),-25} {m_env.Dir}");
            Log($"  {nameof(m_env.InfoDir),-25} {m_env.InfoDir}");
            Log($"  {nameof(m_env.CacheDir),-25} {m_env.CacheDir}");
            Log($"  {nameof(m_env.ArchiveCacheDir),-25} {m_env.ArchiveCacheDir}");
            Log($"  {nameof(m_env.ReadOnlyArchiveCacheDir),-25} {m_env.ReadOnlyArchiveCacheDir}");
            Log();

            if (m_env.SubEnvironments().Any()) {
                Log($"Sub-Environments:");
                foreach (var subEnv in m_env.SubEnvironments())
                    Log($"  {subEnv.WorkingDir}");
            }
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
            var header = $"{"Type",-5}  {"Hash",-8}  {"Download",8}  {"Size",8}  {"Url_Hash",-8}  {"Url"} {"[Args]"}";
            Log(header.Replace("_", " "));
            Log(Regex.Replace(header, @"[^\s]", "-"));
            var entries =
                from entry in m_env.CacheContent()
                let info = entry.Info
                orderby info.Url.ToString()
                select info;
            foreach (var o in entries) {
                var type = $"{o.Type}";
                var urlHash = $"{LfxUrlId.Create(o.Url).ToString().Substring(0, 8)}";
                var hash = $"{o.Hash.ToString().Substring(0, 8)}";
                var downloadSize = $"{o.DownloadSize?.ToFileSize()}";
                var size = $"{o.Size?.ToFileSize()}";

                Log($"{type,-5}  {hash,8}  {downloadSize,8}  {size,8}  {urlHash,8}  {o.Url} {o.Args}");
            }
            var totalContentSize = entries.Sum(o => o.Size);
            var totalArchiveSize = entries.Sum(o => o.DownloadSize);
            Log($"{entries.Count(), 15} File(s) {totalContentSize?.ToFileSize(), 8} Bytes, {totalArchiveSize?.ToFileSize(), 8} Compressed Bytes");
        }
        public void Show() {

            // parse
            var args = Parse(
                minArgs: 1,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Hash 
               )
            );
            var arg = args[0];
            var justHash = args.IsSet(LfxCmdSwitches.Hash);

            Uri url;
            LfxPath path;

            // try url
            if (!Uri.TryCreate(arg, UriKind.Absolute, out url)) {

                // try path
                if (!m_env.TryGetPath(arg, out path))
                    throw new LfxCmdException($"Couldn't parse '{arg}' as url or path.");

                var info = path.Info;
                if (info == null || info?.HasMetadata == false)
                    throw new LfxCmdException($"Path '{path}' has no associated info.");
                url = info?.Url;
            }
                
            // just print hash
            if (justHash) {
                Log($"{LfxUrlId.Create(url)}");
                return;
            }

            LfxArchiveId hash;
            if (!m_env.TryGetArchiveId(url, out hash))
                throw new LfxCmdException($"No archive hash found for url '{url}'.");

            // try get info
            var infos = m_env.GetInfos(hash).ToArray();
            if (!infos.Any())
                throw new LfxCmdException($"No pointer cached for url '{url}'.");

            Log(string.Join(Environment.NewLine + Environment.NewLine, infos.ToArray()));
        }

        public Task Pull() {
            // pull --exe tools\git https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""
            // pull --zip packages\Xamarin.Forms https://www.nuget.org/api/v2/package/Xamarin.Forms/2.3.2.127
            // pull nuget.exe https://dist.nuget.org/win-x86-commandline/v3.5.0/NuGet.exe
            return FetchOrPull(isPull: true);
        }
        public Task Fetch() {
            // fetch --exe https://github.com/git-for-windows/git/releases/download/v2.10.1.windows.1/PortableGit-2.10.1-32-bit.7z.exe "-y -gm2 -InstallPath=\"{0}\""
            // fetch --zip f:\git\lfx-test\.lfx\packages\Xamarin.Android.Support.Animated.Vector.Drawable
            return FetchOrPull(isPull: false);
        }
        private async Task FetchOrPull(bool isPull) {

            var minArgs = isPull ? 2 : 1;
            var urlArgIndex = isPull ? 1 : 0;

            // parse
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
            var isNuget = args.IsSet(LfxCmdSwitches.Nuget);

            // get args
            var url = new Uri(args[urlArgIndex]);
            var exeArgs = isExe ? args[urlArgIndex + 1] : null;

            // make pointer
            var pointer =
                isExe ? LfxPointer.CreateExe(url, exeArgs) :
                isZip ? LfxPointer.CreateZip(url) :
                isNuget ? LfxPointer.CreateNuget(url) :
                LfxPointer.CreateFile(url);

            // log progress
            if (!isQuiet)
                LogProgress();

            // load!
            var entry = await m_env.GetOrLoadContentAsync(pointer);

            // fetch
            if (!isPull) {
                Log($"{entry.Info}");
                return;
            }

            // ensure pulling only done in lfx subdirectory
            var path = m_env.GetPath(Path.GetFullPath(args[0]));

            // add alias to cached content (wha-bam!)
            path.MakeAliasOf(entry);
        }

        private IEnumerable<LfxPath> GetPaths(string arg, bool recurse, bool local) {
            var path = arg;
            if (path == null)
                path = Environment.CurrentDirectory.ToDir();

            path = path.GetFullPath();

            LfxPath lfxPath;
            if (!m_env.TryGetPath(path, out lfxPath)) {

                // all paths in all environments
                if (arg == null && 
                    m_env.RootDir != null && // working dir puts us in an lfx env
                    path.IsSubPathOf(m_env.RootDir) && // path in lfx env
                    !path.IsSubPathOf(m_env.Dir)) // path is not in lfx directory
                    return local ? m_env.LocalPaths() : m_env.AllPaths();

                throw new LfxCmdException($"Path '{path}' has no associated lfx metadata.");
            }

            // file or archive
            if (lfxPath.IsContent)
                return new[] { lfxPath };

            // directory
            return lfxPath.Paths(recurse: recurse);
        }
        public void Ls() {
            var args = Parse(
                minArgs: 0,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.R,
                    LfxCmdSwitches.Alias,
                    LfxCmdSwitches.Local
               )
            );

            var recurse = args.IsSet(LfxCmdSwitches.R);
            var local = args.IsSet(LfxCmdSwitches.Local);
            var showAliases = args.IsSet(LfxCmdSwitches.Alias);

            var paths = GetPaths(args.FirstOrDefault(), recurse, local).ToList();

            // log header
            Log(LfxLsPathRow.Header);

            // group by directory
            var pathsByDir = paths.GroupBy(o => o.Directory).ToList();

            // log paths grouped by directory
            foreach (var group in pathsByDir) {
                Log($"{group.Key}");
                foreach (var o in group)
                    Log(new LfxLsPathRow(LfxPathEx.Create(m_env, o)));
                Log();
            }

            // log footer
            IEnumerable<LfxPath> pathsWithoutMetadata;
            foreach (var counts in m_env.GetLoadEffort(paths, out pathsWithoutMetadata).Where(o => o.Bytes > 0))
                Log($"{counts.Count,15} File(s) {counts.Bytes.ToFileSize(),10} {counts.Action}");
        }
        public async Task Checkout() {
            // checkout

            // parse args
            var args = Parse(
                minArgs: 0,
                maxArgs: 1,
                switchInfo: GitCmdSwitchInfo.Create(
                    LfxCmdSwitches.Quite, LfxCmdSwitches.Q,
                    LfxCmdSwitches.Verbose, LfxCmdSwitches.V,
                    LfxCmdSwitches.Serial,
                    LfxCmdSwitches.Local,
                    LfxCmdSwitches.R
               )
            );
            var isQuiet = args.IsSet(LfxCmdSwitches.Q, LfxCmdSwitches.Quite);
            var isVerbose = args.IsSet(LfxCmdSwitches.V, LfxCmdSwitches.Verbose);
            var isSerial = args.IsSet(LfxCmdSwitches.Serial);
            var local = args.IsSet(LfxCmdSwitches.Local);
            var recurse = args.IsSet(LfxCmdSwitches.R);

            // todo: compute diff to update. for now, nuke lfx
            //m_env.Dir.DeletePath(force: true);
            var paths = GetPaths(args.FirstOrDefault(), recurse, local).ToList();

            // log progress
            LfxProgressTracker progress = null;
            Task progressTask = Task.FromResult(true);
            if (!isQuiet) {
                IEnumerable<LfxPath> pathsWithoutMetadata;
                var counts = m_env.GetLoadEffort(paths, out pathsWithoutMetadata);
                progress = LogProgress(counts, isVerbose);
            }

            // pull content and create aliases
            var restoreTask = paths
                .ParallelForEachAsync(async path => {

                    if (path.IsExtra) {
                        path.Path.DeletePath(force: true);
                        return;
                    }

                    if (path.IsDirectory)
                        return;

                    // fetch content
                    var content = await m_env.GetOrLoadContentAsync((LfxInfo)path.Info);

                    // link alias to content (wha-bam!)
                    path.MakeAliasOf(content);
                },
                maxDegreeOfParallelism: isSerial ? (int?)1 : null
            );

            // await compleation
            await restoreTask.JoinWith(progressTask);
        }
    }

    public struct LfxPathEx {

        public static LfxPathEx Create(LfxEnv env, LfxPath path) {

            if (!path.IsContent)
                return new LfxPathEx(path, null, LfxLoadAction.None, string.Empty);

            var info = (LfxInfo)path.Info;
            var action = env.GetLoadAction((LfxInfo)path.Info);
            string alias = null;

            LfxContent content;
            if (env.TryGetContent(info, out content)) {
                if (path.Path.IsPathAliasOf(content.Path))
                    alias = content.Path;
                info = content.Info;
            }

            return new LfxPathEx(path, info, action, alias);
        }

        private readonly LfxPath m_path;
        private readonly LfxInfo? m_info;
        private readonly LfxLoadAction m_action;
        private readonly string m_alias;

        public LfxPathEx(
            LfxPath path,
            LfxInfo? info,
            LfxLoadAction action,
            string alias) {

            m_path = path;
            m_info = info;
            m_action = action;
            m_alias = alias;
        }

        public bool IsContent => m_path.IsContent;
        public LfxInfo? Info => m_info;
        public LfxPath Path => m_path;
        public LfxLoadAction Action => m_action;
        public string Alias => m_alias;

        public string Name => m_path.Name;
        public bool IsExtra => m_path.IsExtra;
        public bool IsMissing => m_path.IsMissing;
        public bool IsFresh => m_alias != null;
        public bool IsStale => !IsFresh && !IsExtra && !IsMissing;

        public override string ToString() => m_path;
    }

    public struct LfxLsPathRow {
        private const int WidthOfType = 5;
        private const int WidthOfHash = 8;
        private const int WidthOfDownloadSize = 8;
        private const int WidthOfSize = 9;
        private const int WidthOfDiff = 6;
        private const int WidthOfSync = 6;
        private const int WidthOfAction = 6;

        private const string Unknown = "?";
        private const int ShortHashLength = 8;
        private const string ActionWan = "w";
        private const string ActionLan = "l";
        private const string ActionBus = "b";
        private const string ActionCopy = "c";
        private const string ActionExpand = "x";
        private const string ActionAlias = "a";
        private const string ActionNone = " ";
        private const string DiffRemove = "remove";
        private const string DiffAdd = "add";
        private const string DiffUpdate = "update";

        public static string Header {
            get {
                var header = string.Join("  ", new[] {
                    $"{"Type", -WidthOfType}",
                    $"{"__Hash__", WidthOfHash}",
                    $"{"Download", WidthOfDownloadSize}" +
                    $"{"Size", WidthOfSize}",
                    $"{"_Diff_", WidthOfDiff}",
                    $"{"Action", WidthOfAction}",
                    $"{"Name"}"
                }).Replace("_", " ");
                var underline = Regex.Replace(header, @"[^\s]", "-");
                return header + Environment.NewLine + underline;
            }
        }

        private readonly LfxPathEx m_pathEx;

        public LfxLsPathRow(
            LfxPathEx path) {

            m_pathEx = path;
        }

        private bool IsContent => m_pathEx.IsContent;
        private LfxInfo? Info => m_pathEx.Info;

        public string Name => m_pathEx.Name;
        public string Hash {
            get {
                if (!IsContent)
                    return string.Empty;
                return Info?.Hash?.ToString().Substring(0, ShortHashLength) ?? Unknown;
            }
        }
        public string DownloadSize {
            get {
                if (!IsContent)
                    return string.Empty;
                return Info?.DownloadSize?.ToFileSize() ?? Unknown;
            }
        }
        public string Size {
            get {
                if (!IsContent)
                    return string.Empty;
                return Info?.Size?.ToFileSize() ?? Unknown;
            }
        }
        public string Type {
            get {
                if (!IsContent)
                    return string.Empty;

                var type = Info?.Type;
                if (type == LfxType.None)
                    return string.Empty;

                return type.ToString();
            }
        }
        public string Diff {
            get {
                if (m_pathEx.IsExtra)
                    return DiffRemove;

                if (m_pathEx.IsMissing)
                    return DiffAdd;

                if (m_pathEx.IsStale)
                    return DiffUpdate;

                return string.Empty;
            }
        }
        public string Action {
            get {
                var sb = new StringBuilder();
                if (IsContent) {
                    sb.Append(((m_pathEx.Action & LfxLoadAction.DownloadMask) == LfxLoadAction.Wan) ? ActionWan : ActionNone);
                    sb.Append(((m_pathEx.Action & LfxLoadAction.DownloadMask) == LfxLoadAction.Lan) ? ActionLan : ActionNone);
                    sb.Append(((m_pathEx.Action & LfxLoadAction.DownloadMask) == LfxLoadAction.Bus) ? ActionBus : ActionNone);
                    sb.Append(((m_pathEx.Action & LfxLoadAction.ExpandMask) == LfxLoadAction.Copy) ? ActionCopy : ActionNone);
                    sb.Append(((m_pathEx.Action & LfxLoadAction.ExpandMask) == LfxLoadAction.Expand) ? ActionExpand : ActionNone);
                    sb.Append(!m_pathEx.IsFresh ? ActionAlias : ActionNone);
                }
                return sb.ToString();
            }
        }

        public override string ToString() => ToString(false);
        public string ToString(bool showAlias = false) {
            var result = string.Join("  ", new[] {
                $"{Type, -WidthOfType}",
                $"{Hash, WidthOfHash}",
                $"{DownloadSize, WidthOfDownloadSize}" +
                $"{Size, WidthOfSize}",
                $"{Diff, WidthOfDiff}",
                $"{Action, WidthOfAction}",
                $"{Name}"
            });

            if (showAlias && m_pathEx.IsFresh)
                result += $" -> {m_pathEx.Alias}";

            return result;
        }
    }
}