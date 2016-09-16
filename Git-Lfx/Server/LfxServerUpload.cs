using System;
using System.Net;
using System.IO;

namespace Lfx {

    public class LfxServerUpload {

        public LfxServerUpload() { }

        public string SendResponse(HttpListenerRequest request) {
            Console.WriteLine();
            Console.WriteLine(request.HttpMethod);
            Console.WriteLine(request.Url);

            foreach (var o in request.Headers)
                Console.WriteLine($"{o}: {request.Headers[o.ToString()]}");

            var body = new StreamReader(request.InputStream).ReadToEnd();
            Console.WriteLine(body);

            return "";
        }
    }
}