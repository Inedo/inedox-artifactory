#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensions;
using Inedo.Otter.Web.Controls;
#endif
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensions.Artifactory.SuggestionProviders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Promote Build")]
    [Description("Change the status of a build, optionally moving or copying the build's artifacts and its dependencies to a target repository and setting properties on promoted artifacts.")]
    [ScriptAlias("Promote-Build")]
    public sealed class PromoteBuildOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Build name")]
        [ScriptAlias("BuildName")]
        [SuggestibleValue(typeof(BuildSuggestionProvider))]
        public string BuildName { get; set; }

        [Required]
        [DisplayName("Build number")]
        [ScriptAlias("BuildNumber")]
        public string BuildNumber { get; set; }

        [Required]
        [ScriptAlias("Status")]
        public string Status { get; set; }

        [ScriptAlias("Comment")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string Comment { get; set; }

        [ScriptAlias("Properties")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IReadOnlyDictionary<string, RuntimeValue> Properties { get; set; }

        [Category("Move Artifacts")]
        [DisplayName("From repository")]
        [ScriptAlias("FromRepository")]
        [SuggestibleValue(typeof(RepositorySuggestionProvider))]
        public string FromRepository { get; set; }

        [Category("Move Artifacts")]
        [DisplayName("To repository")]
        [ScriptAlias("ToRepository")]
        [SuggestibleValue(typeof(RepositorySuggestionProvider))]
        public string ToRepository { get; set; }

        [Category("Move Artifacts")]
        [DisplayName("Copy instead of moving")]
        [ScriptAlias("Copy")]
        public bool Copy { get; set; }

        [Category("Move Artifacts")]
        [DisplayName("Dependency scopes")]
        [ScriptAlias("Scopes")]
        [PlaceholderText("eg. @(compile, runtime)")]
        public IEnumerable<string> Scopes { get; set; }

        private static readonly Dictionary<string, MessageLevel> MessageLevels = new Dictionary<string, MessageLevel>(StringComparer.OrdinalIgnoreCase)
        {
            { "ERROR", MessageLevel.Error },
            { "WARN", MessageLevel.Warning },
            { "INFO", MessageLevel.Information },
            { "DEBUG", MessageLevel.Debug },
            { "TRACE", MessageLevel.Debug },
        };

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var request = new Request
            {
                Status = this.Status,
                Comment = this.Comment,
                DryRun = context.Simulation,
                SourceRepo = this.FromRepository,
                TargetRepo = this.ToRepository,
                Copy = this.Copy,
                Scopes = this.Scopes,
                Properties = this.Properties.ToDictionary(p => p.Key, p => p.Value.AsEnumerable().Select(v => v.AsString()))
            };

            await this.PostAsync($"api/build/promote/{Uri.EscapeUriString(this.BuildName)}/{Uri.EscapeUriString(this.BuildNumber)}", request, async response =>
            {
                var result = await this.ParseResponseAsync<BuildResult>(response).ConfigureAwait(false);
                if (result.Messages != null)
                {
                    foreach (var message in result.Messages)
                    {
                        this.Log(MessageLevels.ContainsKey(message.Level) ? MessageLevels[message.Level] : MessageLevel.Warning, message.Message);
                    }
                }
            }, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var simple = new RichDescription("Promote ", new Hilite(config[nameof(BuildName)]), " (", new Hilite(config[nameof(BuildNumber)]), ") (Status: ", new Hilite(config[nameof(Status)]), ")");
            var extended = new RichDescription();
            if (!string.IsNullOrEmpty(config[nameof(FromRepository)]))
            {
                if (string.Equals(config[nameof(Copy)], "true", StringComparison.OrdinalIgnoreCase))
                {
                    extended.AppendContent("copy artifacts");
                }
                else
                {
                    extended.AppendContent("move artifacts");
                }
                if (config[nameof(Scopes)].AsEnumerable().Any())
                {
                    extended.AppendContent(" and dependencies (", string.Join(", ", config[nameof(Scopes)].AsEnumerable()), ")");
                }
                extended.AppendContent(" from ", new Hilite(config[nameof(FromRepository)]));
                if (!string.IsNullOrEmpty(config[nameof(ToRepository)]))
                {
                    extended.AppendContent(" to ", new Hilite(config[nameof(ToRepository)]));
                }
            }
            return new ExtendedRichDescription(simple, extended);
        }

        private struct BuildResult
        {
            [JsonProperty(PropertyName = "messages", Required = Required.Always)]
            public IEnumerable<BuildMessage> Messages { get; set; }
        }

        private struct BuildMessage
        {
            [JsonProperty(PropertyName = "level", Required = Required.Always)]
            public string Level { get; set; }
            [JsonProperty(PropertyName = "message", Required = Required.Always)]
            public string Message { get; set; }
        }

        private struct Request
        {
            [JsonProperty(PropertyName = "status", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string Status { get; set; }
            [JsonProperty(PropertyName = "comment", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string Comment { get; set; }
            [JsonProperty(PropertyName = "ciUser", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string CiUser => typeof(PromoteBuildOperation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
            [JsonProperty(PropertyName = "timestamp", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string TimeStamp => DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'+0000'");
            [JsonProperty(PropertyName = "dryRun", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public bool DryRun { get; set; }
            [JsonProperty(PropertyName = "sourceRepo", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string SourceRepo { get; set; }
            [JsonProperty(PropertyName = "targetRepo", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string TargetRepo { get; set; }
            [JsonProperty(PropertyName = "copy", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public bool Copy { get; set; }
            [JsonProperty(PropertyName = "dependencies", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public bool Dependencies => this.Scopes?.Any() ?? false;
            [JsonProperty(PropertyName = "scopes", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<string> Scopes { get; set; }
            [JsonProperty(PropertyName = "properties", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public IReadOnlyDictionary<string, IEnumerable<string>> Properties { get; set; }
        }
    }
}
