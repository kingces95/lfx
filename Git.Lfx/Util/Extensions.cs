using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;

namespace Git.Lfx {

    public static class Extensions {
        public static Task DownloadAsync(this Uri uri, string file) {
            return new WebClient().DownloadFileTaskAsync(uri, file);
        }
        public static async Task<TempFile> DownloadToTempFileAsync(this Uri url, string extension = "") {
            var tempFile = Path.GetTempFileName() + extension;
            await url.DownloadAsync(tempFile);
            return new TempFile(tempFile);
        }
        public static async Task<TempDir> DownloadAndUnZipAsync(this Uri url) {
            using (var tempFile = await DownloadToTempFileAsync(url)) {
                var tempDir = new TempDir();
                ZipFile.ExtractToDirectory(tempFile, tempDir); // todo: make async
                return tempDir;
            }
        }
        public static async Task<TempDir> DownloadAndSelfExtractAsync(this Uri url, string args = null) {
            var result = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            using (var tempDir = new TempDir()) {
                var path = Path.Combine(tempDir, Path.GetRandomFileName() + ".exe");
                await url.DownloadAsync(path);
                Cmd.Execute(path, args, tempDir); // todo: make async
                var tempSubDir = Directory.GetDirectories(tempDir).Single();
                Directory.Move(tempSubDir, result);
            }

            return new TempDir(result);
        }

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
        public static TempDir DownloadAndSelfExtract(this Uri url, string args = null) {
            var result = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            using (var tempDir = new TempDir()) {
                var path = Path.Combine(tempDir, Path.GetRandomFileName() + ".exe");
                url.Download(path);
                Cmd.Execute(path, args, tempDir);
                var tempSubDir = Directory.GetDirectories(tempDir).Single();
                Directory.Move(tempSubDir, result);
            }

            return new TempDir(result);
        }

        public static string MakeRelativePath(this string source, string path) {
            return source.GetDir().ToUrl().MakeRelativeUri(path.ToUrl()).ToString();
        }

        public static char ToLower(this char value) => char.ToLower(value);
        public static string CamelCaseToLispCase(this string value) {
            value = value.ToLowerFirst();
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder();
            foreach (var c in value) {
                var lowerChar = c.ToLower();
                if (c != lowerChar)
                    sb.Append('-');
                sb.Append(lowerChar);
            }

            return sb.ToString();
        }
        public static string ToLowerFirst(this string value) {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder();

            sb.Append(value[0].ToLower());
            if (value.Length > 1)
                sb.Append(value.Substring(1));

            return sb.ToString();
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