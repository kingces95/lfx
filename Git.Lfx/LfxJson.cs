using System;
using System.Collections.Generic;
using Newtonsoft.Json;
// https://github.com/github/git-lfx/blob/master/docs/api/v1/http-v1-batch.md

namespace Git.Lfx.Json {

    public enum LfxJsonOperation {
        Upload,
        Download,
        Unknown
    }

    public enum LfxJsonCode {
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

    public sealed class LfxJsonException : Exception {
        private readonly LfxJsonCode m_code;
        private readonly string m_message;

        public LfxJsonException(LfxJsonCode code, string message = null) {
            m_code = code;
            m_message = message;
        }

        public LfxJsonError Serialize() {
            return new LfxJsonError {
                code = (int)m_code,
                message = m_message
            };
        }
    }

    public sealed class LfxJsonRequest {
        [JsonProperty(Required = Required.Always)]
        public string operation { get; set; }

        public List<LfxJsonObject> objects { get; set; }
    }
    public sealed class LfxJsonResponse {
        public List<LfxJsonObject> objects { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string message { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string request_id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string documentation_url { get; set; }
    }
    public sealed class LfxJsonObject {
        [JsonProperty(Required = Required.Always)]
        public string oid { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int size { get; set; }

        // upload, verfiy, download
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfxJsonActions actions { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfxJsonError error { get; set; }
    }
    public sealed class LfxJsonActions {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfxJsonAction upload { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfxJsonAction verify { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public LfxJsonAction download { get; set; }
    }
    public sealed class LfxJsonError {
        public int code { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string message { get; set; }
    }
    public sealed class LfxJsonAction {
        [JsonProperty(Required = Required.Always)]
        public string href { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string header { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string expires_at { get; set; }
    }
}