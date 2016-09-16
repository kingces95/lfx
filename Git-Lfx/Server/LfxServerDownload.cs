using System;
using System.Net;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Lfx {

    public class LfsServerDownload {
        private Dictionary<string, string> m_filesByHash;

        private static string ComputHash(string path) {
            var sha = SHA256.Create();
            var sb = new StringBuilder();
            using (var fs = File.OpenRead(path)) {
                var bytes = sha.ComputeHash(fs);

                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
            }

            var hash = sb.ToString();
            return hash;
        }

        public LfsServerDownload(string rootDir) {
            m_filesByHash = (
                from file in Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories)
                orderby file descending
                select new {
                    file,
                    hash = ComputHash(file)
                } into pair
                group pair by pair.hash into g
                select new {
                    hash = g.Key,
                    file = g.First().file
                }
            ).ToDictionary(o => o.hash, o => o.file);

            Console.WriteLine("Known files:");
            foreach (var pair in m_filesByHash) {
                Console.WriteLine($"  {pair.Key}: {pair.Value}");
            }
        }

        public object SendResponse(HttpListenerRequest request) {
            Console.WriteLine();
            Console.WriteLine(request.HttpMethod);
            Console.WriteLine(request.Url);

            foreach (var o in request.Headers)
                Console.WriteLine($"{o}: {request.Headers[o.ToString()]}");

            var body = new StreamReader(request.InputStream).ReadToEnd();
            Console.WriteLine(body);

            var oid = request.Url.Segments.Last();
            var path = m_filesByHash[oid];
            Console.WriteLine($"oid: {oid}");
            Console.WriteLine($"file: {path}");

            var bytes = File.ReadAllBytes(path);
            return bytes;
        }
    }
}