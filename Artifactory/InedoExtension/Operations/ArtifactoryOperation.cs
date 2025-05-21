using System.ComponentModel;
using System.Security;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Artifactory.Credentials;
using Inedo.Web;
using Newtonsoft.Json;

namespace Inedo.Extensions.Artifactory.Operations
{
    [ScriptNamespace("Artifactory", PreferUnqualified = false)]
    public abstract class ArtifactoryOperation : ExecuteOperation
    {
        public abstract string CredentialName { get; set; }

        [DisplayName("Base URL")]
        [Category("Credentials")]
        [ScriptAlias("BaseUrl")]
        public string BaseUrl { get; set; }

        [DisplayName("User name")]
        [Category("Credentials")]
        [ScriptAlias("UserName")]
        public string UserName { get; set; }

        [FieldEditMode(FieldEditMode.Password)]
        [DisplayName("Password or API key")]
        [Category("Credentials")]
        [ScriptAlias("Password")]
        public SecureString Password { get; set; }

        protected HttpClient CreateClient()
        {
            return new ArtifactoryCredentials { ServiceUrl = this.BaseUrl, UserName = this.UserName, Password = this.Password }.CreateClient();
        }

        protected async Task PostAsync(string path, object payload, Func<HttpResponseMessage, Task> handleResponse, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var client = this.CreateClient())
            using (var content = new StringContent(JsonConvert.SerializeObject(payload), InedoLib.UTF8Encoding, "application/json"))
            using (var response = await client.PostAsync(path, content, cancellationToken).ConfigureAwait(false))
            {
                await handleResponse(response).ConfigureAwait(false);
            }
        }
    }
}
