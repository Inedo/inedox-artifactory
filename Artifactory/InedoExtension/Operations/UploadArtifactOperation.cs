using System.ComponentModel;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Web;
using Newtonsoft.Json;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Upload Artifact")]
    [Description("Deploy an artifact to an Artifactory repository.")]
    [ScriptAlias("Upload-Artifact")]
    public sealed class UploadArtifactOperation : ArtifactoryOperation
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
        [DisplayName("File to upload")]
        [ScriptAlias("FromFile")]
        public string FromFile { get; set; }

        [Output]
        [DisplayName("Output variable")]
        [PlaceholderText("%ArtifactData")]
        [ScriptAlias("Output")]
        public IDictionary<string, RuntimeValue> Output { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            using (var file = await fileOps.OpenFileAsync(this.FromFile, FileMode.Open, FileAccess.Read).ConfigureAwait(false))
            using (var client = this.CreateClient())
            using (var request = new StreamContent(file))
            using (var response = await client.PutAsync($"{this.RepositoryKey.Trim('/')}/{this.PathToArtifact.Trim('/')}", request, context.CancellationToken).ConfigureAwait(false))
            {
                var result = await this.ParseResponseAsync<Result>(response).ConfigureAwait(false);

                this.LogDebug($"Artifact uploaded to {result.Uri}");

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
                new RichDescription("Upload ", new Hilite(config[nameof(RepositoryKey)]), "::", new Hilite(config[nameof(PathToArtifact)])),
                new RichDescription("from ", new Hilite(config[nameof(FromFile)]))
            );
        }

        internal struct Result
        {
            [JsonProperty(PropertyName = "uri", NullValueHandling = NullValueHandling.Ignore)]
            public string Uri { get; set; }
            [JsonProperty(PropertyName = "downloadUri", NullValueHandling = NullValueHandling.Ignore)]
            public string DownloadUri { get; set; }
            [JsonProperty(PropertyName = "repo", NullValueHandling = NullValueHandling.Ignore)]
            public string Repo { get; set; }
            [JsonProperty(PropertyName = "path", NullValueHandling = NullValueHandling.Ignore)]
            public string Path { get; set; }
            public DateTimeOffset? Created
            {
                get
                {
                    return this.CreatedText == null ? null : (DateTimeOffset?)DateTimeOffset.Parse(this.CreatedText);
                }
                set
                {
                    this.CreatedText = value?.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'+0000'");
                }
            }
            [JsonProperty(PropertyName = "created", NullValueHandling = NullValueHandling.Ignore)]
            public string CreatedText { get; set; }
            [JsonProperty(PropertyName = "createdBy", NullValueHandling = NullValueHandling.Ignore)]
            public string CreatedBy { get; set; }
            [JsonProperty(PropertyName = "size", NullValueHandling = NullValueHandling.Ignore)]
            public string Size { get; set; } // decimal integer bytes
            [JsonProperty(PropertyName = "mimeType", NullValueHandling = NullValueHandling.Ignore)]
            public string MimeType { get; set; }
            [JsonProperty(PropertyName = "checksums", Required = Required.Always)]
            public Checksum Checksums { get; set; }

            internal struct Checksum
            {
                [JsonProperty(PropertyName = "md5", Required = Required.Always)]
                public string Md5 { get; set; }
                [JsonProperty(PropertyName = "sha1", Required = Required.Always)]
                public string Sha1 { get; set; }
            }
        }
    }
}
