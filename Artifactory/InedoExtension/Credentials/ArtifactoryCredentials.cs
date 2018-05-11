using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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
    public sealed class ArtifactoryCredentials : ResourceCredentials
    {
        [Persistent]
        [Required]
        [DisplayName("Base URL")]
        [PlaceholderText("https://example.jfrog.io/example/")]
        public string BaseUrl { get; set; }

        [Persistent]
        [DisplayName("User name")]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [FieldEditMode(FieldEditMode.Password)]
        [DisplayName("Password or API key")]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(new Hilite(this.UserName), " @ ", new Hilite(this.BaseUrl));
        }

        internal HttpClient CreateClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                Credentials = new NetworkCredential(this.UserName, this.Password),
                PreAuthenticate = true
            })
            {
                BaseAddress = new Uri(this.BaseUrl.TrimEnd('/') + '/'),
                DefaultRequestHeaders =
                {
                    UserAgent =
                    {
                        new ProductInfoHeaderValue(typeof(ArtifactoryCredentials).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, typeof(ResourceCredentials).Assembly.GetName().Version.ToString()),
                        new ProductInfoHeaderValue(typeof(ArtifactoryCredentials).Assembly.GetName().Name, typeof(ArtifactoryCredentials).Assembly.GetName().Version.ToString()),
                    }
                }
            };
        }
    }
}
