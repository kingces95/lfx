using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

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
}