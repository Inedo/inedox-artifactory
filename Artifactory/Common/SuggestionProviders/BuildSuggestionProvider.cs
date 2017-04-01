#if BuildMaster
using Inedo.BuildMaster.Extensibility;
#elif Otter
using Inedo.Otter.Extensibility;
#endif
using Inedo.Extensions.Artifactory.Credentials;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.Artifactory.SuggestionProviders
{
    public sealed class BuildSuggestionProvider : ArtifactorySuggestionProvider
    {
        protected override async Task<IEnumerable<string>> GetSuggestionsAsync(ArtifactoryCredentials credentials, IComponentConfiguration config)
        {
            using (var client = credentials.CreateClient())
            using (var response = await client.GetAsync("api/build").ConfigureAwait(false))
            {
                var builds = await this.ParseResponseAsync<BuildCollection>(response).ConfigureAwait(false);
                return builds.Builds?.Select(b => b.Uri.Trim('/'));
            }
        }

        private struct BuildCollection
        {
            [JsonProperty(PropertyName = "uri")]
            public string Uri { get; set; }
            [JsonProperty(PropertyName = "builds", Required = Required.Always)]
            public IEnumerable<Build> Builds { get; set; }
        }
        private struct Build
        {
            [JsonProperty(PropertyName = "uri")]
            public string Uri { get; set; }
            [JsonProperty(PropertyName = "lastStarted")]
            public DateTimeOffset LastStarted { get; set; }
        }
    }
}
