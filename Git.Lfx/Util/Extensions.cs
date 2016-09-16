using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;

namespace Git.Lfx {

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

        public static LfxHash ComputeHash(this string value) => LfxHash.Compute(value);
        public static LfxHash ComputeHash(this Stream stream) => LfxHash.Compute(stream);
        public static LfxHash ComputeHash(this byte[] bytes) => LfxHash.Compute(bytes);
        public static LfxHash ComputeHash(this byte[] bytes, int count) => LfxHash.Compute(bytes, count);

        public static T ToEnum<T>(this string value, bool ignoreCase = false) {
            if (value == null)
                return (T)Enum.ToObject(typeof(T), 0);
            return (T)Enum.Parse(typeof(T), value, ignoreCase);
        }
    }
}