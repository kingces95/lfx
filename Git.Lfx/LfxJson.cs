using System;
using System.Collections.Generic;
using Newtonsoft.Json;
// https://github.com/github/git-lfs/blob/master/docs/api/v1/http-v1-batch.md

namespace Git.Lfs.Json {

    public enum LfsJsonOperation {
        Upload,
        Download,
        Unknown
    }

    public enum LfsJsonCode {
        Success = 200,

        ObjectDoesNotExist = 404,
        ObjectRemovedByOwner = 410,

        // Server MUST validate OID are SHA-256 strings
        // Server MUST validate that size are positive integers
        // Server MAY set an upper bound for object size
        ValidationError = 422,

        AuthenticationRequired = 401,
        WriteAccessDenied = 403,
        RepositoryNotFound = 404,

        BadAcceptHeader = 406,
        RateLimitExceeded = 429,
        NotYetImplemented = 501,
        OutOfStorage = 507,
        BandwidthLimitExceeded = 509
    }

    public sealed class LfsJsonException : Exception {
        private readonly LfsJsonCode m_code;
        private readonly string m_message;

        public LfsJsonException(LfsJsonCode code, string message = null) {
            m_code = code;
            m_message = message;
        }

        public LfsJsonError Serialize() {
            return new LfsJsonError {
                code = (int)m_code,
                message = m_message
            };
        }
    }

    public sealed class LfsJsonRequest {
        [JsonProperty(Required = Required.Always)]
        public string operation { get; set; }

        public List<LfsJsonObject> objects { get; set; }
    }
    public sealed class LfsJsonResponse {
        public List<LfsJsonObject> objects { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string message { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string request_id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string documentation_url { get; set; }
    }
    public sealed class LfsJsonObject {
        [JsonProperty(Required = Required.Always)]
        public string oid { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int size { get; set; }

        // upload, verfiy, download
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfsJsonActions actions { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfsJsonError error { get; set; }
    }
    public sealed class LfsJsonActions {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfsJsonAction upload { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfsJsonAction verify { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfsJsonAction download { get; set; }
    }
    public sealed class LfsJsonError {
        public int code { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string message { get; set; }
    }
    public sealed class LfsJsonAction {
        [JsonProperty(Required = Required.Always)]
        public string href { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string header { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string expires_at { get; set; }
    }
}