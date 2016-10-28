using System.IO;
using System.Threading.Tasks;
using System;

namespace Util {

    public sealed class FileLock : IDisposable {
        private const int MinPollingIntervalMilliseconds = 8;
        private const int MaxPollingIntervalMilliseconds = 1024;
        private const int DefaultTimeoutMilliseconds = 1024 * 5;

        public async static Task<IDisposable> Create(
            string path, int timeoutMilliseconds = DefaultTimeoutMilliseconds) {

            FileStream fileStream = null;
            int delay = MinPollingIntervalMilliseconds;

            var now = DateTime.UtcNow;

            while (DateTime.Now - now < TimeSpan.FromMilliseconds(timeoutMilliseconds)) {

                try {
                    fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
                    return new FileLock(fileStream);
                } 
                
                catch {
                    fileStream?.Dispose();
                }

                await Task.Delay(delay);

                // exponential polling backoff
                if (delay * 2 <= MaxPollingIntervalMilliseconds)
                    delay *= 2;
            }

            throw new TimeoutException(
                $"Timed out after waiting '{timeoutMilliseconds}ms' to aquire file '{path}'.");
        }

        private readonly FileStream m_fileStream;

        public FileLock(FileStream fileStream) {
            m_fileStream = fileStream;
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
            m_fileStream.Dispose();
        }
    }

    public sealed class ImmutablePathDictionary : IDisposable {
        private const string LockFileName = ".lock";
        private const string TempDirName = ".temp";

        private const int L1PartitionCount = 2;
        private const int L2PartitionCount = 2;
        private const int MinimumHashLength = L1PartitionCount + L2PartitionCount;

        private readonly string m_dir;
        private readonly TempDir m_tempDir;
        private readonly string m_lockPath;

        public ImmutablePathDictionary(string dir) {
            m_dir = dir.ToDir();
            m_tempDir = new TempDir(Path.Combine(dir, TempDirName, Path.GetRandomFileName()));
            m_lockPath = Path.Combine(m_dir, LockFileName);
        }

        private string CreateTempPath() {
            return Path.Combine(m_tempDir, Path.GetRandomFileName());
        }
        private void CheckPath(string path) {

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (!path.PathExists())
                throw new ArgumentException($"Path '{path}' does not exist as a file or directory.");
        }
        private async Task<string> CopyToTemp(string path) {
            return await path.CopyPathToAsync(CreateTempPath(), onProgress: OnCopyProgress);
        }
        private async Task<string> MatchPartition(string path) {

            // test if source and target on same partition
            if (path.PathRootEquals(m_dir))
                return path;

            return await CopyToTemp(path);
        }
        private async Task<string> MoveOrLinkIntoStore(string pathOnPartition, string cachePath, bool hardLink = false) {

            using (var fileLock = await FileLock.Create(m_lockPath)) {

                // winner?
                if (!cachePath.PathExists()) {

                    if (hardLink)
                        pathOnPartition.AliasPath(cachePath);

                    else
                        pathOnPartition.MovePath(cachePath);
                }
            }

            return cachePath;
        }

        public event Action<long> OnCopyProgress;

        public string Dir => m_dir;
        public string TempDir => m_tempDir.ToString();

        public async Task<string> PutText(string text, string hash) {

            // check if already created
            string cachePath;
            if (TryGetValue(hash, out cachePath))
                return cachePath;

            if (text == null)
                text = string.Empty;

            // write text to temp file
            var tempPath = CreateTempPath();
            File.WriteAllText(tempPath, text);

            // move temp file into store
            return await MoveOrLinkIntoStore(tempPath, cachePath);
        }
        public async Task<string> Put(string path, string hash, bool preferHardLink = false) {

            CheckPath(path);

            // check if already created
            string cachePath;
            if (TryGetValue(hash, out cachePath))
                return cachePath;

            // hardLink?
            if (preferHardLink && path.PathRootEquals(cachePath))
                return await MoveOrLinkIntoStore(path, cachePath, hardLink: true);

            // store!
            return await MoveOrLinkIntoStore(await CopyToTemp(path), cachePath);
        }


        public async Task<string> Take(string path, string hash) {

            CheckPath(path);

            // check if already created
            string cachePath;
            if (TryGetValue(hash, out cachePath))
                return cachePath;

            // store!
            await MoveOrLinkIntoStore(await MatchPartition(path), cachePath);

            // delete original
            path.DeletePath();

            return cachePath;
        }
        public bool Contains(string hash) {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            // check if already created
            string cachePath;
            return TryGetValue(hash, out cachePath);
        }
        public bool TryGetValue(string hash, out string cachePath) {

            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            if (hash.Length < MinimumHashLength)
                throw new ArgumentException(
                    $"Hash '{hash}' must be at least '{MinimumHashLength}' characters long.");

            cachePath = Path.Combine(
                m_dir,
                hash.ToString().Substring(0, L1PartitionCount),
                hash.ToString().Substring(L1PartitionCount, L2PartitionCount),
                hash
            );

            return cachePath.PathExists();
        }

        public void Clear() {
            Clean();
            Directory.Delete(m_dir);
            Directory.CreateDirectory(m_dir);
        }
        public void Clean() {
            foreach (var directory in m_tempDir.Path.GetDirectories())
                Directory.Delete(directory);
        }

        public void Dispose() {
            m_tempDir.Dispose();
        }

        public override string ToString() => $"{m_dir}";
    }
}