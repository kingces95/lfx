using System;
using System.Net;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Git.Lfx.Json;

namespace Lfx {

    public class LfxServer {
        public string m_url;
        public string m_downloadUrl;
        public string m_uploadUrl;

        public LfxServer(string url, string downloadUrl, string uploadUrl) {
            m_url = url;
            m_downloadUrl = downloadUrl;
            m_uploadUrl = uploadUrl;
        }

        private LfxJsonAction SendUploadResponse(string oid, int size) {
            return null;
            //return new LfxAction {
            //    href = m_uploadUrl,
            //};
        }
        private LfxJsonAction SendDownloadResponse(string oid, int size) {
            return new LfxJsonAction {
                href = m_downloadUrl + oid,
            };
        }
        private LfxJsonObject SendResponse(LfxJsonOperation operation, LfxJsonObject lfxObject) {
            var oid = lfxObject.oid;
            var size = lfxObject.size;
            var upload = operation == LfxJsonOperation.Upload ? SendUploadResponse(oid, size) : null;
            var download = operation == LfxJsonOperation.Download ? SendDownloadResponse(oid, size) : null;

            return new LfxJsonObject {
                oid = oid,
                size = size,
                actions = new LfxJsonActions {
                    upload = upload,
                    download = download
                }
            };
        }
        private LfxJsonOperation DeserializeOperation(string jsonOperation) {
            jsonOperation = jsonOperation.ToLower();
            var operation = jsonOperation == "upload" ? LfxJsonOperation.Upload :
                jsonOperation == "download" ? LfxJsonOperation.Download :
                LfxJsonOperation.Unknown;
            if (operation == LfxJsonOperation.Unknown)
                throw new Exception($"Unknown lfx operation '{jsonOperation}'.");
            return operation;
        }
        private LfxJsonResponse SendResponse(LfxJsonRequest lfxRequest) {
            var operation = DeserializeOperation(lfxRequest.operation);
            return new LfxJsonResponse {
                objects = lfxRequest.objects
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

            var lfxResponses = JsonConvert.SerializeObject(
                SendResponse(JsonConvert.DeserializeObject<LfxJsonRequest>(body)),
                Formatting.Indented
            );

            Console.WriteLine(lfxResponses);

            return lfxResponses;
        }
    }
}