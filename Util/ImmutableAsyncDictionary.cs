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
    public sealed class ConstDirectory :
        IDisposable,
        IEnumerable<string> {

        public delegate void ProgressDelegate(string sourcePath, string targetPath, long? bytes);

        private const string LockFileName = ".lock";
        private const string TempDirName = ".temp";

        private readonly string m_dir;
        private readonly string m_globalTempDir;
        private readonly Lazy<TempDir> m_tempDir;
        private readonly string m_lockPath;
        private readonly Func<string, string> m_keyToCachePath;

        public ConstDirectory(string dir, Func<string, string> keyToCachePath = null) {
            m_dir = dir.ToDir();
            m_globalTempDir = Path.Combine(dir, TempDirName).ToDir();
            m_lockPath = Path.Combine(m_dir, LockFileName);
            m_keyToCachePath = keyToCachePath ?? (o => o);

            // if read-only then cannot create tempDir
            m_tempDir = new Lazy<TempDir>(() =>
                new TempDir(Path.Combine(m_globalTempDir, Path.GetRandomFileName()).ToDir())
            );
        }

        private string KeyToCachePath(string key) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var path = m_keyToCachePath(key);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException($"Key '{key}' transformed to empty path.");

            if (!path.PathIsRooted())
                path = Path.Combine(m_dir, path);

            if (!path.IsSubPathOf(m_dir))
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

            // check if path already created
            string path;
            if (TryGetPath(key, out path))
                return path;

            path = KeyToCachePath(key);

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
                var tempPath = GetTempPath();
                stagingPath = await sourcePath.CopyPathToAsync(
                    target: tempPath, 
                    onProgress: progress => OnCopyProgress(sourcePath, tempPath, progress)
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

        public event ProgressDelegate OnCopyProgress;

        public string Dir => m_dir;
        public string TempDir => m_tempDir.Value.ToString();
        public string GetTempPath() {
            return Path.Combine(m_tempDir.Value, Path.GetRandomFileName());
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
            if (path.PathExistsAsDirectory()) {
                path = path.ToDir();

            } else if (!path.PathExistsAsFile()) {
                path = null;
                return false;
            }

            return true;
        }

        public void Clear() {
            m_dir.DeletePath();
        }
        public void Clean() {
            foreach (var directory in m_globalTempDir.GetDirectories().Except(new[] { m_tempDir.Value.Path }))
                directory.DeletePath(force: true);
        }

        public void Dispose() {
            if (m_tempDir.IsValueCreated)
                m_tempDir.Value.Dispose();
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
    public delegate Task<string> LoadFileAsyncDelegate(string key, string tempPath);
    public sealed class AsyncSelfLoadingDirectory : IEnumerable<string> {

        public delegate void ProgressDelegate(string sourcePath, string targetPath, long? bytes);

        private readonly AsyncSelfLoadingDictionary<string, string> m_dictionary;
        private readonly ConstDirectory m_directory;

        public AsyncSelfLoadingDirectory(string dir, Func<string, string> keyToCachePath = null) {
            m_dictionary = new AsyncSelfLoadingDictionary<string, string>();
            m_dictionary.OnTryLoad += TryLoadHandler;
            m_dictionary.OnTryLoadAsync += TryLoadAsyncHandler;

            m_directory = new ConstDirectory(dir, keyToCachePath);
            m_directory.OnCopyProgress += (sourcePath, targetPath, progress) => 
                OnCopyProgress?.Invoke(sourcePath, targetPath, progress);
        }

        private async Task<string> RaiseTryAsyncLoadEvent(string key, string tempPath) {
            if (OnTryLoadAsync == null)
                return null;

            foreach (LoadFileAsyncDelegate o in 
                OnTryLoadAsync?.GetInvocationList() ?? Enumerable.Empty<Delegate>()) {

                var resultPath = await o(key, tempPath);
                if (resultPath == null)
                    continue;

                return resultPath;
            }

            return null;
        }

        private bool TryLoadHandler(string key, out string path) {
            return m_directory.TryGetPath(key, out path);
        }
        private async Task<KeyValuePair<bool, string>> TryLoadAsyncHandler(string key) {

            // async load content given key
            var path = await RaiseTryAsyncLoadEvent(key, GetTempPath());
            if (path == null)
                return new KeyValuePair<bool, string>(false, default(string));

            // cache content to disk by key
            var contentPath = path.IsSubPathOf(TempDir) ?
                await m_directory.Move(path, key) :
                await m_directory.Copy(path, key, preferAlias: true);

            // cache content in memory by key
            return new KeyValuePair<bool, string>(true, contentPath);
        }

        public string Dir => m_directory.Dir;
        public string TempDir => m_directory.TempDir;
        public string GetTempPath() => m_directory.GetTempPath();

        public event ProgressDelegate OnCopyProgress;
        public event LoadFileAsyncDelegate OnTryLoadAsync;

        public bool Contains(string key) {
            string path;
            return TryGetPath(key, out path);
        }
        public bool TryGetPath(string key, out string path) {
            return m_directory.TryGetPath(key, out path);
        }
        public async Task<string> TryGetOrLoadPathAsync(string key) {
            var result = await m_dictionary.TryGetOrLoadValueAsync(key);

            // unpackage result
            if (!result.Key)
                return null;
            return result.Value;
        }

        public void Clean() {
            m_directory.Clean();
        }

        public IEnumerator<string> GetEnumerator() => m_directory.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}