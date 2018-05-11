using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Artifactory.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Get Artifact Metadata")]
    [Description("Retrieve metadata for an artifact from an Artifactory repository.")]
    [ScriptAlias("Get-Artifact-Metadata")]
    public sealed class GetArtifactMetadataOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Repository")]
        [ScriptAlias("Repository")]
        [SuggestableValue(typeof(RepositorySuggestionProvider))]
        public string RepositoryKey { get; set; }

        [Required]
        [DisplayName("Path to artifact")]
        [ScriptAlias("Path")]
        public string PathToArtifact { get; set; }

        [Output]
        [Required]
        [DisplayName("Output variable")]
        [PlaceholderText("%ArtifactData")]
        [ScriptAlias("Output")]
        public IDictionary<string, RuntimeValue> Output { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            using (var client = this.CreateClient())
            using (var response = await client.GetAsync($"api/storage/{this.RepositoryKey.Trim('/')}/{this.PathToArtifact.Trim('/')}", HttpCompletionOption.ResponseHeadersRead, context.CancellationToken).ConfigureAwait(false))
            {
                var result = await this.ParseResponseAsync<UploadArtifactOperation.Result>(response).ConfigureAwait(false);

                this.LogDebug($"Artifact is at {result.Uri}");

                this.Output = new Dictionary<string, RuntimeValue>
                {
                    { "type", Path.GetExtension(this.PathToArtifact).Trim('.') },
                    { "sha1", result.Checksums.Sha1 },
                    { "md5", result.Checksums.Md5 },
                    { "name", Path.GetFileName(this.PathToArtifact) }
                };
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Get metadata for ", new Hilite(config[nameof(RepositoryKey)]), "::", new Hilite(config[nameof(PathToArtifact)]))
            );
        }
    }
}
