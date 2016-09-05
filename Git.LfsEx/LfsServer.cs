using System;
using System.Net;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace Git.Lfs {

    public class LfsServer {
        public string m_url;
        public string m_downloadUrl;
        public string m_uploadUrl;

        public LfsServer(string url, string downloadUrl, string uploadUrl) {
            m_url = url;
            m_downloadUrl = downloadUrl;
            m_uploadUrl = uploadUrl;
        }

        private LfsJsonAction SendUploadResponse(string oid, int size) {
            return null;
            //return new LfsAction {
            //    href = m_uploadUrl,
            //};
        }
        private LfsJsonAction SendDownloadResponse(string oid, int size) {
            return new LfsJsonAction {
                href = m_downloadUrl + oid,
            };
        }
        private LfsJsonObject SendResponse(LfsJsonOperation operation, LfsJsonObject lfsObject) {
            var oid = lfsObject.oid;
            var size = lfsObject.size;
            var upload = operation == LfsJsonOperation.Upload ? SendUploadResponse(oid, size) : null;
            var download = operation == LfsJsonOperation.Download ? SendDownloadResponse(oid, size) : null;

            return new LfsJsonObject {
                oid = oid,
                size = size,
                actions = new LfsJsonActions {
                    upload = upload,
                    download = download
                }
            };
        }
        private LfsJsonOperation DeserializeOperation(string jsonOperation) {
            jsonOperation = jsonOperation.ToLower();
            var operation = jsonOperation == "upload" ? LfsJsonOperation.Upload :
                jsonOperation == "download" ? LfsJsonOperation.Download :
                LfsJsonOperation.Unknown;
            if (operation == LfsJsonOperation.Unknown)
                throw new Exception($"Unknown lfs operation '{jsonOperation}'.");
            return operation;
        }
        private LfsJsonResponse SendResponse(LfsJsonRequest lfsRequest) {
            var operation = DeserializeOperation(lfsRequest.operation);
            return new LfsJsonResponse {
                objects = lfsRequest.objects
                    .Select(o => SendResponse(operation, o)).ToList()
            };
        }

        public string SendResponse(HttpListenerRequest request) {
            Console.WriteLine();
            Console.WriteLine(request.HttpMethod);
            Console.WriteLine(request.Url);

            foreach (var o in request.Headers)
                Console.WriteLine($"{o}: {request.Headers[o.ToString()]}");

            var body = new StreamReader(request.InputStream).ReadToEnd();
            Console.WriteLine(body);

            var lfsResponses = JsonConvert.SerializeObject(
                SendResponse(JsonConvert.DeserializeObject<LfsJsonRequest>(body)),
                Formatting.Indented
            );

            Console.WriteLine(lfsResponses);

            return lfsResponses;
        }
    }
}