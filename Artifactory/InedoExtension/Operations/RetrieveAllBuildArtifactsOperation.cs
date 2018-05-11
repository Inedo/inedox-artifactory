using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Artifactory.SuggestionProviders;
using Inedo.Web;
using Inedo.Web.Plans.ArgumentEditors;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Retrieve All Build Artifacts")]
    [Description("Retrieves a zip file containing all artifacts related to a specific build.")]
    [ScriptAlias("Retrieve-All-Build-Artifacts")]
    public sealed class RetrieveAllBuildArtifactsOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Build name")]
        [ScriptAlias("BuildName")]
        [SuggestableValue(typeof(BuildSuggestionProvider))]
        public string BuildName { get; set; }

        [Required]
        [DisplayName("Build number")]
        [Description("For the latest build, enter LATEST")]
        [ScriptAlias("BuildNumber")]
        public string BuildNumber { get; set; }

        [ScriptAlias("Status")]
        [DisplayName("Status")]
        [PlaceholderText("Optionally search by latest build status (e.g: \"Released\")")]
        public string Status { get; set; }

        [Required]
        [DisplayName("Write to file")]
        [ScriptAlias("ToFile")]
        [FilePathEditor]
        public string ToFile { get; set; }

        [DisplayName("Mappings")]
        [ScriptAlias("Mappings")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Example(@"Mappings: @(%(input: `(.+`)/`(.+`)-sources.jar, output: `$1/sources/`$2.jar), %(input: `(.+`)-release.zip))")]
        public IEnumerable<IReadOnlyDictionary<string, string>> Mappings { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var request = new Request
            {
                BuildName = this.BuildName,
                BuildNumber = this.BuildNumber,
                BuildStatus = AH.NullIf(this.Status, string.Empty),
                Mappings = ConvertMappings(this.Mappings)
            };

            await this.PostAsync("api/archive/buildArtifacts", request, async response =>
            {
                using (var content = await this.ParseResponseAsync(response).ConfigureAwait(false))
                using (var output = await fileOps.OpenFileAsync(this.ToFile, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
                {
                    await content.CopyToAsync(output).ConfigureAwait(false);
                }
            }, context.CancellationToken).ConfigureAwait(false);
        }

        private IEnumerable<Request.Mapping> ConvertMappings(IEnumerable<IReadOnlyDictionary<string, string>> mappings)
        {
            if (!(mappings?.Any() ?? false))
            {
                return null;
            }

            foreach (var mapping in mappings)
            {
                if (!mapping.ContainsKey("input"))
                {
                    this.LogWarning("Mapping missing \"input\" key.");
                }
                foreach (var key in mapping.Keys)
                {
                    if (key != "input" && key != "output")
                    {
                        this.LogWarning($"Mapping contains unknown key \"{key}\".");
                    }
                }
            }
            return mappings.Where(m => m.ContainsKey("input")).Select(m => new Request.Mapping { Input = m["input"], Output = m.ContainsKey("output") ? m["output"] : null });
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string buildNumber = config[nameof(BuildNumber)];
            if ((buildNumber ?? string.Empty).Equals("LATEST", StringComparison.OrdinalIgnoreCase))
            {
                buildNumber = $"Latest {(string)config[nameof(Status)] ?? string.Empty} Build";
            }
            return new ExtendedRichDescription(
                new RichDescription("Retrieve all artifacts from ", new Hilite(config[nameof(BuildName)]), " (", new Hilite(buildNumber), ")"),
                new RichDescription("to ", new Hilite(config[nameof(ToFile)]))
            );
        }

        private struct Request
        {
            [JsonProperty(PropertyName = "buildName", Required = Required.Always)]
            public string BuildName { get; set; }
            [JsonProperty(PropertyName = "buildNumber", Required = Required.Always)]
            public string BuildNumber { get; set; }
            [JsonProperty(PropertyName = "buildStatus", NullValueHandling = NullValueHandling.Ignore)]
            public string BuildStatus { get; set; }
            [JsonProperty(PropertyName = "archiveType", Required = Required.Always)]
            public string ArchiveType => "zip";
            [JsonProperty(PropertyName = "mappings", NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<Mapping> Mappings { get; set; }

            internal struct Mapping
            {
                [JsonProperty(PropertyName = "input", Required = Required.Always)]
                public string Input { get; set; }
                [JsonProperty(PropertyName = "output", NullValueHandling = NullValueHandling.Ignore)]
                public string Output { get; set; }
            }
        }
    }
}
