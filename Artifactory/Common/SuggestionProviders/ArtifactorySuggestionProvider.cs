#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Web.Controls;
#endif
using Inedo.Extensions.Artifactory.Credentials;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using System;

namespace Inedo.Extensions.Artifactory.SuggestionProviders
{
    public abstract class ArtifactorySuggestionProvider : ISuggestionProvider, ILogger
    {
        public event EventHandler<LogMessageEventArgs> MessageLogged;

        public void Log(MessageLevel logLevel, string message)
        {
            this.MessageLogged?.Invoke(this, new LogMessageEventArgs(logLevel, message));
        }

        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentials = ResourceCredentials.Create<ArtifactoryCredentials>(config[nameof(IHasCredentials<ArtifactoryCredentials>.CredentialName)]);
            var baseUrl = AH.CoalesceString(config[nameof(ArtifactoryCredentials.BaseUrl)], credentials?.BaseUrl);
            var userName = AH.CoalesceString(config[nameof(ArtifactoryCredentials.UserName)], credentials?.UserName);
            var password = CoalescePassword(config[nameof(ArtifactoryCredentials.Password)], credentials?.Password);

            return GetSuggestionsAsync(new ArtifactoryCredentials { BaseUrl = baseUrl, UserName = userName, Password = password }, config);
        }

        protected abstract Task<IEnumerable<string>> GetSuggestionsAsync(ArtifactoryCredentials credentials, IComponentConfiguration config);

        private static SecureString CoalescePassword(string a, SecureString b)
        {
#if BuildMaster
            return AH.CoalesceString(a, b?.ToUnsecureString())?.ToSecureString();
#elif Otter
            return AH.CreateSecureString(AH.CoalesceString(a, AH.Unprotect(b)));
#endif
        }
    }
}
