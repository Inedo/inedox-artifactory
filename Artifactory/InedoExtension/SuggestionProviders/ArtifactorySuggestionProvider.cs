using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Artifactory.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.Artifactory.SuggestionProviders
{
    public abstract class ArtifactorySuggestionProvider : ISuggestionProvider, ILogger, ILogSink
    {
        public event EventHandler<LogMessageEventArgs> MessageLogged;

        public void Log(MessageLevel logLevel, string message)
        {
            this.MessageLogged?.Invoke(this, new LogMessageEventArgs(logLevel, message));
        }

        public void Log(IMessage message)
        {
            this.MessageLogged?.Invoke(this, new LogMessageEventArgs(message.Level, message.Message, message.Category, message.Details, message.ContextData));
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
            return AH.CreateSecureString(AH.CoalesceString(a, AH.Unprotect(b)));
        }
    }
}
