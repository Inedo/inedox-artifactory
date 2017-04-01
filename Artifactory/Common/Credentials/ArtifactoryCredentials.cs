#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions;
#endif
using Inedo.Documentation;
using Inedo.Serialization;
using System.Security;
using System.ComponentModel;
using System;
using System.Reflection;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Http;

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
