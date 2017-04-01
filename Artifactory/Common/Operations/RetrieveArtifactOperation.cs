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
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensions.Artifactory.SuggestionProviders;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Retrieve Artifact")]
    [ScriptAlias("Retrieve-Artifact")]
    public sealed class RetrieveArtifactOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Repository")]
        [ScriptAlias("Repository")]
        [SuggestibleValue(typeof(RepositorySuggestionProvider))]
        public string RepositoryKey { get; set; }

        [Required]
        [DisplayName("Path to artifact")]
        [ScriptAlias("Path")]
        public string PathToArtifact { get; set; }

        [Required]
        [DisplayName("Write to file")]
        [ScriptAlias("ToFile")]
        [FilePathEditor]
        public string ToFile { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            using (var client = this.CreateClient())
            using (var response = await client.GetAsync($"{this.RepositoryKey.Trim('/')}/{this.PathToArtifact.Trim('/')}", HttpCompletionOption.ResponseHeadersRead, context.CancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    this.LogError($"{(int)response.StatusCode} {response.ReasonPhrase}");
                    return;
                }

                using (var file = await fileOps.OpenFileAsync(this.ToFile, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
                {
                    await response.Content.CopyToAsync(file).ConfigureAwait(false);
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Retrieve ", new Hilite(config[nameof(RepositoryKey)]), "::", new Hilite(config[nameof(PathToArtifact)])),
                new RichDescription("to ", new Hilite(config[nameof(ToFile)]))
            );
        }
    }
}
