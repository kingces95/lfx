using Git.Lfx;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Lfx {

    public class Program {
        public const string Url = "http://192.168.0.70/lfx/";
        public const string DownloadUrl = Url + "download/";
        public const string UploadUrl = Url + "upload/";

        public static void Main() {
            LfxCmd.Execute(Environment.CommandLine);
        }

        private static void MainWebServer(string[] args) {
            var lsu = new LfxServerUpload();
            var lsuws = new WebServer("Lfx Upload", lsu.SendResponse, UploadUrl);
            lsuws.Run();

            var lsd = new LfxServerDownload(@"f:\nugit\");
            var lsdws = new WebServer("Lfx Download", lsd.SendResponse, DownloadUrl);
            lsdws.Run();

            var ls = new LfxServer(Url, DownloadUrl, UploadUrl);
            var lsws = new WebServer("Lfx", ls.SendResponse, Url);
            lsws.Run();

            Console.WriteLine("A simple webserver. Press a key to quit.");
            Console.ReadKey();
            lsuws.Stop();
            lsdws.Stop();
            lsws.Stop();
        }
    }
}