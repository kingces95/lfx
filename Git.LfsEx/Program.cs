using Git.Lfs;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace Git.Lfs {
    class Program {
        public const string Url = "http://192.168.0.70/lfs/";
        public const string DownloadUrl = Url + "download/";
        public const string UploadUrl = Url + "upload/";
        public const string SwitchPrefix = "-";

        static void Main(string[] args) {
            var switches = new HashSet<string>(
                args.Where(o => o.StartsWith(SwitchPrefix))
                    .Select(o => o.Substring(SwitchPrefix.Length)),
                StringComparer.InvariantCultureIgnoreCase
            );
            var arguments = new List<string>(
                args.Where(o => !o.StartsWith(SwitchPrefix))
            ).ToArray();

            if (args.Length == 0)
                MainWebServer(args);

            else if (args[0] == "clean")
                MainClean(args);

            else if (args[0] == "smudge")
                MainSmudge(args);

            else if (args[0] == "fetch")
                MainFetch(arguments, switches);
        }

        static void MainClean(string[] args) {
            var loader = LfsLoader.Create();
            var file = loader.GetFile(args[1]);
            var pointer = file.Pointer;
            Console.Write(pointer);
        }

        static void MainFetch(string[] args, HashSet<string> switches) {
            var url = args[1].ToUrl();

            if (switches.Contains("zip"))
                FetchZip(url);
        }
        static void FetchZip(Uri url) {
            var appDataDir = Environment.GetEnvironmentVariable("APPDATA") + Path.DirectorySeparatorChar;
            var gitCurlDir = appDataDir + "gitcurl" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(gitCurlDir);

            var gitCurlZipDir = gitCurlDir + "zip" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(gitCurlZipDir);

            var urlHash = url.ToString().ComputeHash();
            var zipFile = gitCurlZipDir + urlHash + ".zip";
            var zipFileDir = gitCurlZipDir + urlHash + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(zipFileDir);

            if (!File.Exists(zipFile))
                new WebClient().DownloadFile(url, zipFile);

            ZipFile.ExtractToDirectory(zipFile, zipFileDir);
            foreach (var file in Directory.GetFiles(zipFileDir, "*", SearchOption.AllDirectories)) {
                Console.WriteLine(zipFileDir.ToUrl().MakeRelativeUri(file.ToUrl()));
            }
        }

        static void MainSmudge(string[] args) {
            var fileName = Path.GetFileName(args[1]);

            var pointer = LfsPointer.Parse(Console.In);

            var appDataDir = Environment.GetEnvironmentVariable("APPDATA") + Path.DirectorySeparatorChar;
            var gitCurlDir = appDataDir + "gitcurl" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(gitCurlDir);

            Stream result = null;
            if (pointer.Type == LfsPointerType.Archive) {
                var gitCurlZipDir = gitCurlDir + "zip" + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(gitCurlZipDir);

                var url = pointer.Url.ToString();
                var zipFile = gitCurlZipDir + url.ComputeHash() + ".zip";

                if (!File.Exists(zipFile))
                    new WebClient().DownloadFile(url, zipFile);

                var archive = new ZipArchive(File.OpenRead(zipFile));
                var entry = archive.GetEntry(pointer.Hint.ToString());
                result = entry.Open();
            }

            else if (pointer.Url != null) {
                //nyi
            }

            if (result == null) {
                Console.WriteLine(pointer);
                return;
            }

            result.CopyTo(Console.OpenStandardOutput());
        }

        static void MainWebServer(string[] args) {
            var lsu = new LfsServerUpload();
            var lsuws = new WebServer("Lfs Upload", lsu.SendResponse, UploadUrl);
            lsuws.Run();

            var lsd = new LfsServerDownload(@"f:\nugit\");
            var lsdws = new WebServer("Lfs Download", lsd.SendResponse, DownloadUrl);
            lsdws.Run();

            var ls = new LfsServer(Url, DownloadUrl, UploadUrl);
            var lsws = new WebServer("Lfs", ls.SendResponse, Url);
            lsws.Run();

            Console.WriteLine("A simple webserver. Press a key to quit.");
            Console.ReadKey();
            lsuws.Stop();
            lsdws.Stop();
            lsws.Stop();
        }
    }
}