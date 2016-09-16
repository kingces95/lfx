using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;

namespace Git.Lfs {

    public static class Extensions {
        public static void Download(this Uri uri, string file) {
            new WebClient().DownloadFile(uri, file);
        }
        public static TempFile DownloadToTempFile(this Uri url) {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            url.Download(tempFile);
            return new TempFile(tempFile);
        }
        public static TempDir DownloadAndUnZip(this Uri url) {
            using (var tempFile = DownloadToTempFile(url)) {
                var tempDir = new TempDir();
                ZipFile.ExtractToDirectory(tempFile, tempDir);
                return tempDir;
            }
        }

        public static LfsHash ComputeHash(this string value) => LfsHash.Compute(value);
        public static LfsHash ComputeHash(this Stream stream) => LfsHash.Compute(stream);
        public static LfsHash ComputeHash(this byte[] bytes) => LfsHash.Compute(bytes);
        public static LfsHash ComputeHash(this byte[] bytes, int count) => LfsHash.Compute(bytes, count);

        public static T ToEnum<T>(this string value, bool ignoreCase = false) {
            if (value == null)
                return (T)Enum.ToObject(typeof(T), 0);
            return (T)Enum.Parse(typeof(T), value, ignoreCase);
        }
    }
}