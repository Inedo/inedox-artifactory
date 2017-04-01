#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Plans;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls;
using Inedo.Otter.Web.Controls.Plans;
#endif
using Inedo.Documentation;
using Inedo.Extensions.Artifactory.SuggestionProviders;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Newtonsoft.Json;
using System.IO;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Retrieve All Build Artifacts")]
    [ScriptAlias("Retrieve-All-Build-Artifacts")]
    public sealed class RetrieveAllBuildArtifactsOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Build name")]
        [ScriptAlias("BuildName")]
        [SuggestibleValue(typeof(BuildSuggestionProvider))]
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

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            var request = new Request
            {
                BuildName = this.BuildName,
                BuildNumber = this.BuildNumber,
                BuildStatus = AH.NullIf(this.Status, string.Empty)
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
            [JsonProperty(PropertyName = "buildStatus", Required = Required.DisallowNull)]
            public string BuildStatus { get; set; }
            [JsonProperty(PropertyName = "archiveType", Required = Required.Always)]
            public string ArchiveType => "zip";
        }
    }
}
