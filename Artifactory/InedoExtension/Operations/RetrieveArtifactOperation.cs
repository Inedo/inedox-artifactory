using System.ComponentModel;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Retrieve Artifact")]
    [Description("Retrieves an artifact from the specified destination.")]
    [ScriptAlias("Retrieve-Artifact")]
    public sealed class RetrieveArtifactOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Repository")]
        [ScriptAlias("Repository")]
        public string RepositoryKey { get; set; }

        [Required]
        [DisplayName("Path to artifact")]
        [ScriptAlias("Path")]
        public string PathToArtifact { get; set; }

        [Required]
        [DisplayName("Write to file")]
        [ScriptAlias("ToFile")]
        public string ToFile { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            using (var client = this.CreateClient())
            using (var response = await client.GetAsync($"{this.RepositoryKey.Trim('/')}/{this.PathToArtifact.Trim('/')}", HttpCompletionOption.ResponseHeadersRead, context.CancellationToken).ConfigureAwait(false))
            {
                using (var content = await this.ParseResponseAsync(response).ConfigureAwait(false))
                using (var file = await fileOps.OpenFileAsync(this.ToFile, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
                {
                    await content.CopyToAsync(file).ConfigureAwait(false);
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
