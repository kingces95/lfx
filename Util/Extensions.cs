﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;
using System.Reflection;
using System.Threading.Tasks.Dataflow;

namespace Util {

    internal static class Kernel32 {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool CreateHardLink(
          string lpFileName,
          string lpExistingFileName,
          IntPtr lpSecurityAttributes
        );
    }

    // async with progress
    public static partial class Extensions {
        private const int DefaultBufferSize = 4096 * 8;

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
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            // track last file size for each path
            var fileSizeByPath = new Dictionary<string, long>();

            // report file growth delta
            monitor.InternalBufferSize = 0;
            monitor.Changed += (s, e) => {
                var filePath = e.FullPath;
                if (Directory.Exists(filePath))
                    return;

                try {
                    // race to get FileInfo
                    var fileSize = new FileInfo(e.FullPath).Length;

                    // get and update last file size
                    long lastFileSize;
                    lock (fileSizeByPath) {
                        fileSizeByPath.TryGetValue(filePath, out lastFileSize);
                        fileSizeByPath[filePath] = fileSize;
                    }

                    // report delta in file size!
                    onGrowth?.Invoke(filePath, fileSize - lastFileSize);

                } catch (FileNotFoundException) {
                    // lost race to get FileInfo; E.g. when the windows git-package.exe 
                    // expansion finishes it runs a post-install script which it then
                    // deletes. If we get notified and lose the race to read info with
                    // the delete then a FileNotFoundException is raised.
                }
            };

            return monitor;
        }

        public static Task CopyToAsync(
            this Stream source, 
            Stream target,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            return CopyToAsync(source, new[] { target }, bufferSize, cancellationToken, onProgress);
        }

        public static void CopyTo(
            this Stream source, 
            Stream target,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            CopyToAsync(source, target, bufferSize, cancellationToken, onProgress).Wait();
        }

        public static async Task CopyToAsync(
            this Stream source, 
            Stream[] targets, 
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

        public static async Task<string> ExpandZip(
            this string zipFilePath, 
            string targetDir, 
            Action<long> onProgress = null) {

            await Task.Run(() => {

                var zipFile = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

                foreach (var entry in zipFile.Entries) {
                    // ignore directories
                    if (IOPath.GetFileName(entry.FullName).Length == 0)
                        continue;

                    // throw if unzipping target is outside of targetDir
                    var targetPath = IOPath.Combine(targetDir, entry.FullName);
                    if (!targetPath.StartsWith(targetDir))
                        throw new ArgumentException(
                            $"Zip package '{zipFilePath}' entry '{entry.FullName}' is outside package.");

                    // unzip entry
                    Directory.CreateDirectory(targetPath.GetDir());
                    using (var targeStream = File.OpenWrite(targetPath)) {
                        using (var sourceStream = entry.Open()) {

                            // report progress while unzipping
                            sourceStream.CopyTo(targeStream, onProgress: onProgress);
                        }
                    }
                }
            });

            return targetDir;
        }

        public static async Task<byte[]> DownloadAndHash(
            this Uri url,
            string tempPath,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            var sha256 = SHA256.Create();

            // save to temp file
            using (var fileStream = File.OpenWrite(tempPath)) {

                // compute hash while saving file
                using (var shaStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write)) {

                    // initiate download
                    using (var downloadStream = await new WebClient().OpenReadTaskAsync(url)) {

                        // save downloaded file to temp file while reporting progress
                        await downloadStream.CopyToAsync(shaStream, bufferSize, cancellationToken, onProgress);
                    }
                }
            }

            return sha256.Hash;
        }

        public static async Task<string> CopyPathToAsync(
            this string path, 
            string target,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancellationToken = null,
            Action<long> onProgress = null) {

            if (File.Exists(path)) {
                using (var pathStream = File.Open(path, FileMode.Open))
                    using (var targetStream = File.Create(target))
                        await pathStream.CopyToAsync(targetStream, bufferSize, cancellationToken, onProgress);
            }

            else {
                target = target.ToDir();
                path = path.ToDir();

                Parallel.ForEach(path.GetAllFiles(), file => {
                    var recursiveDir = path.GetRecursiveDir(file);
                    var targetPath = IOPath.Combine(target, recursiveDir, file.GetFileName());
                    CopyPathToAsync(file, targetPath, onProgress: onProgress).Wait();
                });
            }

            return target;
        }

        public static string ToFileSize(this long size) {
            return size.ToStringMetric("b");
        }
        public static string ToStringMetric(this long size, string suffix = null) {

            var abbriviations = new[] { "", "k", "m", "g", "t", "p", "e", "z" };

            int thousand = 1000;
            int i = 0;
            var maxSize = thousand;

            while (size > maxSize) {
                maxSize *= thousand;
                i++;
            }

            return $"{(decimal)size / (maxSize / thousand):0.0}{abbriviations[i]}{suffix}";
        }
    }

    // paths
    public static partial class Extensions {

        // tests
        public static bool IsUncPath(this string path) => new Uri(path).IsUnc;
        public static bool IsDir(this string path) {
            return path.EndsWith($"{IOPath.DirectorySeparatorChar}");
        }
        public static bool EqualPath(this string path, string target) {
            return string.Compare(
                IOPath.GetFullPath(path),
                IOPath.GetFullPath(target),
                ignoreCase: false
            ) == 0;
        }
        public static bool IsSubDirOf(this string path, string basePath) {
            return !basePath.GetRecursiveDir(path).Contains("..");
        }
        public static bool PathRootEquals(this string path, string target) {
            return IOPath.GetPathRoot(path).EqualsIgnoreCase(IOPath.GetPathRoot(target));
        }
        public static bool PathIsFile(this string path) => File.Exists(path);
        public static bool PathIsDirectory(this string path) => Directory.Exists(path);
        public static bool PathExists(this string path) => path.PathIsFile() || path.PathIsDirectory();
        public static bool PathIsRooted(this string path) => IOPath.IsPathRooted(path);

        // transforms
        public static string ToDir(this string dir) {
            if (dir == null)
                return null;

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
        public static string GetRecursiveDir(this string basePath, string subPath) {
            if (!IOPath.IsPathRooted(subPath))
                return subPath.GetDir();

            var relativeUri = new Uri(basePath).MakeRelativeUri(new Uri(subPath));
            var recursivePath = relativeUri.ToString().Replace("/", IOPath.DirectorySeparatorChar.ToString());
            return recursivePath.GetDir();
        }
        public static string GetFileName(this string path) {
            return IOPath.GetFileName(path);
        }
        public static string PathCombine(this string path, params string[] subPath) {
            return IOPath.Combine(new[] { path }.Concat(subPath).ToArray());
        }

        // directory + file
        public static void MakeDeletable(this string file) {
            var info = new FileInfo(file);
            var attributes = info.Attributes;
            var flagsToClear = FileAttributes.ReadOnly | FileAttributes.System;
            if ((attributes & flagsToClear) != 0)
                info.Attributes &= ~flagsToClear;
        }
        public static string MovePath(this string sourcePath, string targetPath) {
            if (sourcePath == null)
                throw new ArgumentNullException(nameof(sourcePath));

            if (targetPath == null)
                throw new ArgumentNullException(nameof(targetPath));

            Directory.CreateDirectory(targetPath.GetDir());

            if (File.Exists(sourcePath))
                File.Move(sourcePath, targetPath);

            else if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, targetPath);

            else
                throw new Exception($"Cannot move non-existant '{sourcePath}' to '{targetPath}'.");

            return targetPath;
        }
        public static bool DeletePath(this string path, bool force = false) {

            // delete file
            if (File.Exists(path)) {
                path.MakeDeletable();
                File.Delete(path);
                return true;
            }

            // delete directory
            if (Directory.Exists(path)) {

                // don't recursively delete content in a junction point
                if (!JunctionPoint.Exists(path)) {

                    // delete subDirs
                    foreach (var subDir in path.GetDirectories())
                        subDir.DeletePath(force);

                    // delete files
                    foreach (var file in path.GetAllFiles())
                        file.DeletePath(force);
                }

                // delete dir
                Directory.Delete(path);
                return true;
            }

            return false;
        }

        // get size
        public static long GetFileSize(this string path) {
            return new FileInfo(path).Length;
        }
        public static long GetDirectorySize(this string path) {
            var dirInfo = new DirectoryInfo(path);
            var size = path.GetAllFileInfos().Sum(o => o.Length);
            return size;
        }
        public static IEnumerable<string> GetFiles(this string path) {
            return Directory.GetFiles(path);
        }
        public static IEnumerable<FileInfo> GetFileInfos(this string path) {
            return new DirectoryInfo(path).GetFiles();
        }
        public static IEnumerable<string> GetDirectories(this string path) {
            return Directory.GetDirectories(path);
        }
        public static IEnumerable<DirectoryInfo> GetDirectoryInfos(this string path) {
            return new DirectoryInfo(path).GetDirectories();
        }
        public static IEnumerable<string> GetAllFiles(this string path) {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        }
        public static IEnumerable<FileInfo> GetAllFileInfos(this string path) {
            return new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);
        }

        // aliases
        public static string AliasPath(this string path, string target) {

            if (target.PathExists())
                target.DeletePath();

            Directory.CreateDirectory(target.GetDir());

            if (path.PathIsFile()) {
                if (!Kernel32.CreateHardLink(target, path, IntPtr.Zero))
                    throw new ArgumentException($"Failed to create hard link '{path}' -> '{target}'.");
            } 
            
            else if (path.PathIsDirectory()) {
                JunctionPoint.Create(path, target, overwrite: true);
            } 
            
            else
                throw new IOException($"The path '{path}' does not exist.");

            return target;
        }
        public static IEnumerable<string> GetPathAliases(this string path) {
            if (path.PathIsFile())
                return HardLinkInfo.GetLinks(path);

            if (path.PathIsDirectory()) {
                if (!JunctionPoint.Exists(path))
                    return Enumerable.Empty<string>();

                return new[] { JunctionPoint.GetTarget(path) };
            }
            
            else throw new IOException($"The path '{path}' does not exist.");
        }

        public static void WriteAllText(this string path, string text) {
            File.WriteAllText(path, text);
        }
    }

    // find ancestor directory
    public static partial class Extensions {
        public static string GetParentDir(this string dir) {
            return Directory.GetParent(dir.GetDir()).ToString().GetDir();
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
    }
    
    // tasks
    public static partial class Extensions {
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
        public static async Task<T> WaitOneAsync<T>(this WaitHandle waitHandle, Func<T> result) {
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

            return await tcs.Task;
        }

        public static Task JoinWith(this Task task, params Task[] tasks) {
            return Task.WhenAll(new[] { task }
                .Concat(tasks ?? Enumerable.Empty<Task>())
                .Where(o => o != null)
            );
        }
    }

    // misc
    public static partial class Extensions {

        // enum
        public static T ToEnum<T>(this string value, bool ignoreCase = false) {
            if (value == null)
                return (T)Enum.ToObject(typeof(T), 0);
            return (T)Enum.Parse(typeof(T), value, ignoreCase);
        }

        // string
        public static bool EqualsIgnoreCase(this string source, string target) {
            return string.Compare(source, target, true) == 0;
        }
        public static string ExpandOrTrim(this string source, int length) {
            if (source.Length > length)
                return source.Substring(0, length);
            return source.PadRight(length);
        }

        // string builder
        public static void AppendLine(this StringBuilder stringBuilder, object value) {
            if (value == null)
                value = string.Empty;

            value = value.ToString();

            stringBuilder.AppendLine((string)value);
        }

        // uri
        public static Uri ToUrl(this string value, UriKind kind = UriKind.Absolute) {
            Uri url;
            if (!Uri.TryCreate(value, kind, out url))
                throw new Exception($"Expected '{value}' to be '{kind}' url.");
            return url;
        }

        // regex
        public static string Get(this Match match, string name) {
            var group = match.Groups[name];
            if (group == null)
                return null;

            var value = group.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            return value;
        }

        // dictionary
        public static V GetValueOrDefault<K, V>(this Dictionary<K, V> source, K key) {
            V value;
            if (!source.TryGetValue(key, out value))
                return default(V);
            return value;
        }

        // stream
        public static Stream ToMemoryStream(this Stream stream) {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms;
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

        // reflection
        public static bool IsOverriden(Type type, string name) {
            var bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var method = type.GetMethod(name, bf);
            return method != method.GetBaseDefinition();
        }

        public static void ParallelForEach<T>(this IEnumerable<T> source, Action<T> action) {
            Parallel.ForEach(source, action);
        }
        public static async Task ParallelForEachAsync<T>(this IEnumerable<T> source, Action<T> action) {
            await Task.Run(() => source.ParallelForEach(action));
        }
        public static async Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action) {

            // checkout
            var actionBlock = new ActionBlock<T>(
                action: async o => await action(o), 
                dataflowBlockOptions: new ExecutionDataflowBlockOptions {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });

            // go!
            foreach (var o in source)
                await actionBlock.SendAsync(o);

            // await compleation
            actionBlock.Complete();
            await actionBlock.Completion;
        }
    }
}