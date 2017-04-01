#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif
using Inedo.Extensions.Artifactory.Credentials;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.Artifactory.SuggestionProviders
{
    public sealed class RepositorySuggestionProvider : ArtifactorySuggestionProvider
    {
        protected override async Task<IEnumerable<string>> GetSuggestionsAsync(ArtifactoryCredentials credentials, IComponentConfiguration config)
        {
            using (var client = credentials.CreateClient())
            using (var response = await client.GetAsync("api/repositories").ConfigureAwait(false))
            {
                var repositories = await this.ParseResponseAsync<IEnumerable<Repository>>(response).ConfigureAwait(false);
                return repositories?.Select(r => r.Key);
            }
        }

        private struct Repository
        {
            [JsonProperty(PropertyName = "key", Required = Required.Always)]
            public string Key { get; set; }
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }
            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }
            [JsonProperty(PropertyName = "url")]
            public string Url { get; set; }
        }
    }
}
