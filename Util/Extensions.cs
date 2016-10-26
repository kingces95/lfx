using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Util {

    internal static class Kernel32 {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool CreateHardLink(
          string lpFileName,
          string lpExistingFileName,
          IntPtr lpSecurityAttributes
        );
    }

    public static class Extensions {
        private const int DefaultBufferSize = 4096 * 8;

        public static T Await<T>(this Task<T> task) {
            task.Wait();
            return task.Result;
        }
        public static Task<TResult> ToApm<TResult>(this Task<TResult> task, AsyncCallback callback, object state) {
            if (task.AsyncState == state) {
                if (callback != null) {
                    task.ContinueWith(
                        delegate { callback(task); },
                        CancellationToken.None, 
                        TaskContinuationOptions.None, 
                        TaskScheduler.Default
                    );
                }
                return task;
            }

            var tcs = new TaskCompletionSource<TResult>(state);
            task.ContinueWith(delegate {
                if (task.IsFaulted)
                    tcs.TrySetException(task.Exception.InnerExceptions);

                else if (task.IsCanceled)
                    tcs.TrySetCanceled();

                else
                    tcs.TrySetResult(task.Result);

                callback?.Invoke(tcs.Task);

            }, CancellationToken.None, 
            TaskContinuationOptions.None,
            TaskScheduler.Default);

            return tcs.Task;
        }

        public static Task WaitOneAsync(this WaitHandle waitHandle) {
            return waitHandle.WaitOneAsync(() => true);
        }
        public static Task<T> WaitOneAsync<T>(this WaitHandle waitHandle, Func<T> result) {
            if (waitHandle == null)
                throw new ArgumentNullException(nameof(waitHandle));

            var tcs = new TaskCompletionSource<T>();

            RegisteredWaitHandle rwh = null;
            rwh = ThreadPool.RegisterWaitForSingleObject(
                waitObject: waitHandle,
                callBack: (s, t) => {
                    rwh.Unregister(null);
                    tcs.TrySetResult(result());
                },
                state: null,
                millisecondsTimeOutInterval: -1,
                executeOnlyOnce: true
            );

            return tcs.Task;
        }

        public static FileSystemWatcher MonitorGrowth(
            this string path, 
            Action<string, long> onGrowth = null, 
            bool isFile = false) {
            var filter = "*";

            // watching a file or directory?
            if (File.Exists(path))
                isFile = true;

            // monitor a single file
            if (isFile) {
                filter = IOPath.GetFileName(path);
                path = path.GetDir();
            }

            // monitor async load progress
            var monitor = new FileSystemWatcher(path, filter) {
                NotifyFilter = 
                    NotifyFilters.Attributes |
                    NotifyFilters.CreationTime |
                    NotifyFilters.DirectoryName |
                    NotifyFilters.FileName |
                    NotifyFilters.LastAccess |
                    NotifyFilters.LastWrite |
                    NotifyFilters.Security |
                    NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            // track last file size for each path
            var fileSizeByPath = new Dictionary<string, long>();

            // report file growth delta
            monitor.InternalBufferSize = 0;
            monitor.Changed += (s, e) => {
                var filePath = e.FullPath;
                var fileSize = new FileInfo(e.FullPath).Length;

                // get and update last file size
                long lastFileSize;
                lock (fileSizeByPath) {
                    fileSizeByPath.TryGetValue(filePath, out lastFileSize);
                    fileSizeByPath[filePath] = fileSize;
                }

                // report delta in file size!
                onGrowth?.Invoke(filePath, fileSize - lastFileSize);
            };

            return monitor;
        }
        public static Task CopyToAsync(this Stream source, Stream target,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            return CopyToAsync(source, new[] { target }, bufferSize, cancellationToken, onProgress);
        }
        public static void CopyTo(this Stream source, Stream target,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            CopyToAsync(source, target, bufferSize, cancellationToken, onProgress).Wait();
        }
        public static async Task CopyToAsync(this Stream source, Stream[] targets, 
            int bufferSize = DefaultBufferSize, 
            CancellationToken? cancellationToken = null, 
            Action<long> onProgress = null) {

            if (cancellationToken == null)
                cancellationToken = CancellationToken.None;

            var buffer = new byte[bufferSize];
            while (true) {
                var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken.Value);
                if (bytesRead == 0)
                    break;

                await Task.WhenAll(
                    targets.Select(o => o.WriteAsync(buffer, 0, bytesRead, cancellationToken.Value))
                );

                onProgress(bytesRead);
            }
        }

        public static bool CanHardLinkTo(this string path, string target) {
            return string.Equals(
                IOPath.GetPathRoot(path),
                IOPath.GetPathRoot(target),
                StringComparison.InvariantCultureIgnoreCase
            );
        }
        public static async Task CopyToAsync(this string path, string target,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            using (var pathStream = File.Open(path, FileMode.Open))
                using (var targetStream = File.Create(target))
                    await pathStream.CopyToAsync(targetStream, bufferSize, cancellationToken, onProgress);
        }
        public static void HardLinkTo(this string path, string target) {
            if (!Kernel32.CreateHardLink(target, path, IntPtr.Zero))
                throw new ArgumentException($"Failed to create hard link {path} => {target}.");
        }

        public static bool IsUncPath(this string path) => new Uri(path).IsUnc;
        public static bool EqualPath(this string path, string target) {
            return string.Compare(
                IOPath.GetFullPath(path),
                IOPath.GetFullPath(target),
                ignoreCase: false
            ) == 0;
        }
        public static string ToDir(this string dir) {
            if (!dir.EndsWith($"{IOPath.DirectorySeparatorChar}"))
                dir += IOPath.DirectorySeparatorChar;
            return dir;
        }
        public static string GetDir(this string dir) {
            var dirName = IOPath.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(dirName))
                return @".\";
            return dirName.ToDir();
        }
        public static string CopyToDir(this string file, string dir = null) {
            if (dir == null)
                dir = Environment.CurrentDirectory;

            var target = IOPath.Combine(
                IOPath.GetFullPath(dir),
                IOPath.GetFileName(file)
            );

            File.Copy(file, target);
            return target;
        }
        public static Uri ToUrl(this string value, UriKind kind = UriKind.Absolute) {
            Uri url;
            if (!Uri.TryCreate(value, kind, out url))
                throw new Exception($"Expected '{value}' to be '{kind}' url.");
            return url;
        }
        public static string GetParentDir(this string dir) {
            return Directory.GetParent(dir.GetDir()).ToString().GetDir();
        }
        public static bool IsDir(this string path) {
            return path.EndsWith($"{IOPath.DirectorySeparatorChar}");
        }

        public static void CopyTo(this StreamReader reader, StreamWriter target) {
            var buffer = new char[4096];
            while (true) {
                var count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count == 0)
                    break;
                target.Write(buffer, 0, count);
            }
        }
        public static IEnumerable<string> Lines(this StreamReader stream) {
            while (true) {
                var line = stream.ReadLine();
                if (string.IsNullOrEmpty(line))
                    yield break;

                yield return line;
            }
        }
        public static IEnumerable<string> Lines(
            this TextReader stream, string delimiter = null, int maxLength = int.MaxValue) {

            if (string.IsNullOrEmpty(delimiter))
                delimiter = Environment.NewLine;

            var sb = new StringBuilder();
            var delimiterIndex = 0;

            while (true) {

                if (delimiter.Length == delimiterIndex || sb.Length == maxLength) {
                    yield return sb.ToString();
                    sb.Clear();
                    delimiterIndex = 0;
                }

                var current = stream.Read();
                if (current == -1)
                    break;

                var currentChar = (char)current;

                if (delimiter[delimiterIndex] == currentChar) {
                    delimiterIndex++;
                    continue;
                }

                sb.Append(currentChar);
            }

            yield return sb.ToString();
        }

        public static string Get(this Match match, string name) {
            var group = match.Groups[name];
            if (group == null)
                return null;

            var value = group.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            return value;
        }

        public static V GetValueOrDefault<K, V>(this Dictionary<K, V> source, K key) {
            V value;
            if (!source.TryGetValue(key, out value))
                return default(V);
            return value;
        }

        public static string FindDirectoryAbove(this string dir, string searchPattern) {
            return dir.FindDirectoriesAbove(searchPattern).SingleOrDefault();
        }
        public static string FindFileAbove(this string dir, string searchPattern) {
            return dir.FindFilesAbove(searchPattern).SingleOrDefault();
        }
        public static string FindPathAbove(this string dir, string searchPattern) {
            return dir.FindPathsAbove(searchPattern).SingleOrDefault();
        }

        public static IEnumerable<string> FindDirectoriesAbove(this string dir, string searchPattern) {
            return new DirectoryInfo(dir.GetDir()).FindDirectoriesAbove(searchPattern).Select(o => o.FullName.ToDir());
        }
        public static IEnumerable<string> FindFilesAbove(this string dir, string searchPattern) {
            return new DirectoryInfo(dir.GetDir()).FindFilesAbove(searchPattern).Select(o => o.FullName);
        }
        public static IEnumerable<string> FindPathsAbove(this string dir, string searchPattern) {
            return new DirectoryInfo(dir.GetDir()).FindPathsAbove(searchPattern).Select(o => o.FullName);
        }

        public static DirectoryInfo FindDirectoryAbove(this DirectoryInfo dir, string searchPattern) {
            return dir.FindDirectoriesAbove(searchPattern).SingleOrDefault();
        }
        public static FileInfo FindFileAbove(this DirectoryInfo dir, string searchPattern) {
            return dir.FindFilesAbove(searchPattern).SingleOrDefault();
        }
        public static FileSystemInfo FindPathAbove(this DirectoryInfo dir, string searchPattern) {
            return dir.FindPathsAbove(searchPattern).SingleOrDefault();
        }

        public static IEnumerable<DirectoryInfo> FindDirectoriesAbove(this DirectoryInfo dir, string searchPattern) {
            return dir.FindPathsAbove(searchPattern).OfType<DirectoryInfo>();
        }
        public static IEnumerable<FileInfo> FindFilesAbove(this DirectoryInfo dir, string searchPattern) {
            return dir.FindPathsAbove(searchPattern).OfType<FileInfo>();
        }
        public static IEnumerable<FileSystemInfo> FindPathsAbove(this DirectoryInfo dir, string searchPattern) {

            if (dir == null)
                yield break;

            if (searchPattern == null)
                yield break;

            while (dir != null) {

                foreach (var info in dir.GetFileSystemInfos(searchPattern))
                    yield return info;

                dir = dir.Parent;
            }
        }

        public static Stream ToMemoryStream(this Stream stream) {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms;
        }
    }
}