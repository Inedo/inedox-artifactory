using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Artifactory.Credentials
{
    [DisplayName("JFrog Artifactory")]
    [Description("Credentials for JFrog Artifactory.")]
    [ScriptAlias("Artifactory")]
    public sealed class ArtifactoryCredentials : ServiceCredentials, IMissingPersistentPropertyHandler
    {
        [Required]
        [DisplayName("Base URL")]
        [PlaceholderText("https://example.jfrog.io/example/")]
        public override string ServiceUrl { get; set; }

        [Persistent]
        [DisplayName("User name")]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [FieldEditMode(FieldEditMode.Password)]
        [DisplayName("Password or API key")]
        public SecureString Password { get; set; }

        public override RichDescription GetCredentialDescription() => new(this.UserName);
        public override RichDescription GetServiceDescription() => new(this.ServiceUrl);

        public override ValueTask<ValidationResults> ValidateAsync(CancellationToken cancellationToken = default) => new();

        internal HttpClient CreateClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                Credentials = new NetworkCredential(this.UserName, this.Password),
                PreAuthenticate = true
            })
            {
                BaseAddress = new Uri(this.ServiceUrl.TrimEnd('/') + '/'),
                DefaultRequestHeaders =
                {
                    UserAgent =
                    {
                        new ProductInfoHeaderValue(SDK.ProductName, SDK.ProductVersion.ToString(3)),
                        new ProductInfoHeaderValue(typeof(ArtifactoryCredentials).Assembly.GetName().Name, typeof(ArtifactoryCredentials).Assembly.GetName().Version.ToString()),
                    }
                }
            };
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (missingProperties.TryGetValue("BaseUrl", out var baseUrl))
                this.ServiceUrl = baseUrl;
        }
    }
}
