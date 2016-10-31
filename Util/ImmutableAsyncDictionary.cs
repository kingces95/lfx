using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Util {

    public delegate Task<KeyValuePair<bool, T>> TryGetValueAsyncDelegate<TKey, T>(TKey key);
    public delegate bool TryGetValueDelegate<TKey, T>(TKey key, out T value);
    public sealed class AsyncSelfLoadingDictionary<TKey, T> {

        private readonly ConcurrentDictionary<TKey, LazyTask<KeyValuePair<bool, T>>> m_values;

        public AsyncSelfLoadingDictionary() {
            m_values = new ConcurrentDictionary<TKey, LazyTask<KeyValuePair<bool, T>>>();
        }

        private async Task<KeyValuePair<bool, T>> RaiseTryLoadAsyncEvent(TKey key) {
            if (OnTryLoadAsync != null) {
                foreach (TryGetValueAsyncDelegate<TKey, T> o in 
                    OnTryLoadAsync?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                    var result = await o(key);
                    if (result.Key)
                        return result;
                }
            }

            return new KeyValuePair<bool, T>(false, default(T));
        }
        private bool RaiseTryLoadEvent(TKey key, out T result) {
            result = default(T);

            if (OnTryLoadAsync == null)
                return false;
                
            foreach (TryGetValueDelegate<TKey, T> o in
                OnTryLoad?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                if (o(key, out result))
                    return true;
            }

            return false;
        }

        public event TryGetValueAsyncDelegate<TKey, T> OnTryLoadAsync;
        public event TryGetValueDelegate<TKey, T> OnTryLoad;

        public bool ContainsKey(TKey key) {
            T value;
            return TryGetValue(key, out value);
        }
        public bool TryGetValue(TKey key, out T value) {
            value = default(T);

            // never loaded?
            LazyTask<KeyValuePair<bool, T>> taskValue;
            if (!m_values.TryGetValue(key, out taskValue))
                return false;

            // load pending?
            if (!taskValue.Value.IsCompleted)
                return false;

            // load failed?
            var pair = taskValue.Value.Result;
            if (!pair.Key)
                return false;

            // successfully loaded!
            value = pair.Value;
            return true;
        }
        public async Task<KeyValuePair<bool, T>> TryGetOrLoadValueAsync(TKey key) {

            return await m_values.GetOrAdd(key, delegate {

                // try synchronous load
                T value = default(T);
                if (RaiseTryLoadEvent(key, out value))
                    return LazyTask.FromResult(new KeyValuePair<bool, T>(true, value));

                // asynchronous load
                return new LazyTask<KeyValuePair<bool, T>>(
                    async () => await RaiseTryLoadAsyncEvent(key)
                );
            });
        }
    }

    /// <summary>
    /// Intended to be used as a cache for a file server whose content is immutable. 
    /// All files placed in cache are marked readonly. Multipule writers to the same 
    /// path are assumed to be racing to cache the same content. The cache does *not*
    /// verify all writes to the same path contain the same content. Directory and
    /// file additions are atomic; Content is first copied to a temp directory on the
    /// same partition as the cache before a file lock is taken under which a move
    /// (or "aliasing" hard link for files or junction for directories) is executed.
    /// </summary>
    public sealed class ImmutableDirectory :
        IDisposable,
        IEnumerable<string> {

        private const string LockFileName = ".lock";
        private const string TempDirName = ".temp";

        private readonly string m_dir;
        private readonly string m_globalTempDir;
        private readonly TempDir m_tempDir;
        private readonly string m_lockPath;
        private readonly Func<string, string> m_keyToCachePath;

        public ImmutableDirectory(string dir, Func<string, string> keyToCachePath = null) {
            m_dir = dir.ToDir();
            m_globalTempDir = Path.Combine(dir, TempDirName);
            m_tempDir = new TempDir(Path.Combine(m_globalTempDir, Path.GetRandomFileName()));
            m_lockPath = Path.Combine(m_dir, LockFileName);
            m_keyToCachePath = keyToCachePath ?? (o => o);
        }

        private string KeyToCachePath(string key) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var path = m_keyToCachePath(key);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException($"Key '{key}' transformed to empty path.");

            if (!path.PathIsRooted())
                path = Path.Combine(m_dir, path);

            if (!path.IsSubDirOf(m_dir))
                throw new Exception(
                    $"Key '{key}' resolved to a path '{path}' outside of cache directory '{m_dir}'");

            return path;
        }
        private async Task<string> CopyOrMove(
            string sourcePath,
            string key,
            bool move = false,
            bool preferAlias = false) {

            if (sourcePath == null)
                throw new ArgumentNullException(nameof(sourcePath));

            if (!sourcePath.PathExists())
                throw new ArgumentException($"Path '{sourcePath}' does not exist as a file or directory.");

            var copy = !move;

            // transform key to path
            var path = KeyToCachePath(key);

            // check if path already created
            if (path.PathExists())
                return path;

            // can only alias when asked to copy to/form the same paration
            if (preferAlias && copy && sourcePath.PathRootEquals(m_dir)) {
                using (var fileLock = await FileLock.Create(m_lockPath)) {
                    if (!path.PathExists())
                        sourcePath.AliasPath(path);
                    return path;
                }
            }

            var stagingPath = sourcePath;

            // copy source if on different partition or for copy operation
            if (!sourcePath.PathRootEquals(m_dir) || copy) {
                stagingPath = await sourcePath.CopyPathToAsync(
                    target: GetTempPath(), 
                    onProgress: OnCopyProgress
                );
            }

            // move stagingPath into cache
            using (var fileLock = await FileLock.Create(m_lockPath)) {
                if (!path.PathExists())
                    stagingPath.MovePath(path);
            }

            // complete move by deleting source
            if (move)
                sourcePath.DeletePath();

            return path;
        }

        public event Action<long> OnCopyProgress;

        public string Dir => m_dir;
        public string TempDir => m_tempDir.ToString();
        public string GetTempPath() {
            return Path.Combine(m_tempDir, Path.GetRandomFileName());
        }

        public Task<string> EchoText(string key, string text) {
            
            // write text to temp file
            var tempPath = GetTempPath();
            File.WriteAllText(tempPath, text);

            return Move(tempPath, key);
        }
        public Task<string> Copy(string sourcePath, string key, bool preferAlias = false) {
            return CopyOrMove(sourcePath, key, move: false, preferAlias: preferAlias);
        }

        public Task<string> Move(string sourcePath, string key) {
            return CopyOrMove(sourcePath, key, move: true);
        }
        public bool Exists(string key) {
            string path;
            return TryGetPath(key, out path);
        }
        public bool TryGetPath(string key, out string path) {
            path = KeyToCachePath(key);
            return path.PathExists();
        }

        public void Clear() {
            m_dir.DeletePath();
        }
        public void Clean() {
            foreach (var directory in m_globalTempDir.GetDirectories().Except(new[] { m_tempDir.Path }))
                directory.DeletePath(force: true);
        }

        public void Dispose() {
            m_tempDir.Dispose();
        }

        public IEnumerator<string> GetEnumerator() {
            var result =
                from dirs in m_dir.GetDirectories().Except(new[] { m_globalTempDir })
                from files in dirs.GetAllFiles()
                orderby files
                select files;

            return result.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"{m_dir}";
    }

    /// <summary>
    /// Intended to be used as a cache for a file server whose content is immutable.
    /// Like an ImmutableDirectory except that the content "load itself"; when content
    /// needs to be loaded the OnTryLoadAsync is raised. OnTryLoadAsync will only be 
    /// called once even if multipule threads race to load the same content. The losers 
    /// block until the winner loads the content.
    /// </summary>
    public delegate Task<string> LoadFileAsyncDelegate(string hash, string tempPath);
    public sealed class AsyncSelfLoadingDirectory {
        private readonly AsyncSelfLoadingDictionary<string, string> m_dictionary;
        private readonly ImmutableDirectory m_directory;

        public AsyncSelfLoadingDirectory(string dir, Func<string, string> keyToCachePath = null) {
            m_dictionary = new AsyncSelfLoadingDictionary<string, string>();
            m_dictionary.OnTryLoad += TryLoadHandler;
            m_dictionary.OnTryLoadAsync += TryLoadAsyncHandler;

            m_directory = new ImmutableDirectory(dir, keyToCachePath);
            m_directory.OnCopyProgress += progress => OnCopyProgress?.Invoke(progress);
        }

        private async Task<string> RaiseTryAsyncLoadEvent(string hash, string tempPath) {
            if (OnTryLoadAsync == null)
                return null;

            foreach (LoadFileAsyncDelegate o in 
                OnTryLoadAsync?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                var resultPath = await o(hash, tempPath);
                if (resultPath == null)
                    continue;

                return resultPath;
            }

            return null;
        }

        private bool TryLoadHandler(string hash, out string path) {
            return m_directory.TryGetPath(hash, out path);
        }
        private async Task<KeyValuePair<bool, string>> TryLoadAsyncHandler(string hash) {

            // async load content given hash
            var path = await RaiseTryAsyncLoadEvent(hash, GetTempPath());
            if (path == null)
                return new KeyValuePair<bool, string>(false, default(string));

            // cache content to disk by hash
            var contentPath = path.IsSubDirOf(TempDir) ?
                await m_directory.Move(path, hash) :
                await m_directory.Copy(path, hash, preferAlias: true);

            // cache content in memory by hash
            return new KeyValuePair<bool, string>(true, contentPath);
        }

        public string Dir => m_directory.Dir;
        public string TempDir => m_directory.TempDir;
        public string GetTempPath() => m_directory.GetTempPath();

        public event Action<long> OnCopyProgress;
        public event LoadFileAsyncDelegate OnTryLoadAsync;

        public bool TryGetPath(string hash, out string path) {
            return m_dictionary.TryGetValue(hash, out path);
        }
        public async Task<string> TryGetOrLoadPathAsync(string hash) {
            var result = await m_dictionary.TryGetOrLoadValueAsync(hash);

            // unpackage result
            if (!result.Key)
                return null;
            return result.Value;
        }

        public void Clean() {
            m_directory.Clean();
        }
    }
}