using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Inedo.Extensions.Artifactory
{
    internal static class RestApi
    {
        public static async Task<T> ParseResponseAsync<T>(this ILogger logger, HttpResponseMessage response)
        {
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            try
            {
                var errors = JsonConvert.DeserializeObject<ArtifactoryErrors>(payload);
                foreach (var error in errors.Errors)
                {
                    logger.LogError(error.ToString());
                }
                return default(T);
            }
            catch
            {
                response.EnsureSuccessStatusCode();

                return JsonConvert.DeserializeObject<T>(payload);
            }
        }

        public static async Task<HttpContent> ParseResponseAsync(this ILogger logger, HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errors = JsonConvert.DeserializeObject<ArtifactoryErrors>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    foreach (var error in errors.Errors)
                    {
                        logger.LogError(error.ToString());
                    }
                }
                catch (JsonException)
                {
                    logger.LogError($"{(int)response.StatusCode} {response.ReasonPhrase}");
                }
                return new ByteArrayContent(new byte[0]);
            }

            return response.Content;
        }

        private struct ArtifactoryErrors
        {
            [JsonProperty(PropertyName = "errors", Required = Required.Always)]
            public IEnumerable<ArtifactoryError> Errors { get; set; }
        }

        private struct ArtifactoryError
        {
            [JsonProperty(PropertyName = "status", Required = Required.Always)]
            public HttpStatusCode Status { get; set; }
            [JsonProperty(PropertyName = "message", Required = Required.Always)]
            public string Message { get; set; }

            public override string ToString()
            {
                return $"{(int)this.Status} {this.Message}";
            }
        }

        public static object AsObject(this RuntimeValue value)
        {
            switch (value.ValueType)
            {
                case RuntimeValueType.Map:
                    return value.AsDictionary().ToDictionary(v => v.Key, v => v.Value.AsObject());
                case RuntimeValueType.Vector:
                    return value.AsEnumerable().Select(v => v.AsObject());
                case RuntimeValueType.Scalar:
                    return value.AsString();
            }
            throw new NotImplementedException();
        }
    }
}
