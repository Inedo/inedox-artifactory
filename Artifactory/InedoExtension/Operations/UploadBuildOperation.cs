using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Artifactory.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.Artifactory.Operations
{
    [DisplayName("Upload Build")]
    [Description("Create a new build in Artifactory.")]
    [ScriptAlias("Upload-Build")]
    public sealed class UploadBuildOperation : ArtifactoryOperation
    {
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [DisplayName("Build name")]
        [ScriptAlias("Name")]
        [SuggestableValue(typeof(BuildSuggestionProvider))]
        public string BuildName { get; set; }

        [Required]
        [DisplayName("Version")]
        [ScriptAlias("Version")]
        [PlaceholderText("$ReleaseNumber")]
        public string BuildVersion { get; set; }

        [Required]
        [DisplayName("Build number")]
        [ScriptAlias("Number")]
        [PlaceholderText("$PackageNumber")]
        public string BuildNumber { get; set; }

        /*
        [DisplayName("Include issues")]
        [ScriptAlias("IncludeIssues")]
        [AppliesTo(InedoProduct.BuildMaster)]
        public bool IncludeIssues { get; set; }
        */

        [DisplayName("Build type")]
        [ScriptAlias("BuildType")]
        [DefaultValue(nameof(BuildType.GENERIC))]
        public BuildType ArtifactoryBuildType { get; set; }

        [DisplayName("Properties")]
        [ScriptAlias("Properties")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IReadOnlyDictionary<string, RuntimeValue> Properties { get; set; }

        [DisplayName("Modules")]
        [ScriptAlias("Modules")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public IEnumerable<RuntimeValue> Modules { get; set; }

        [DisplayName("Source control URL")]
        [Category("Metadata")]
        [ScriptAlias("VcsUrl")]
        public string VcsUrl { get; set; }

        [DisplayName("Source control revision")]
        [Category("Metadata")]
        [ScriptAlias("VcsRevision")]
        public string VcsRevision { get; set; }

        [DisplayName("Agent name")]
        [Category("Metadata")]
        [ScriptAlias("BuildAgentName")]
        public string BuildAgentName { get; set; }

        [DisplayName("Agent version")]
        [Category("Metadata")]
        [ScriptAlias("BuildAgentVersion")]
        public string BuildAgentVersion { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var info = new BuildInfo
            {
                Name = this.BuildName,
                Version = this.BuildVersion,
                Number = this.BuildNumber,
                ArtifactoryPrincipal = this.UserName,
                Type = this.ArtifactoryBuildType,
                Properties = this.Properties?.ToDictionary(p => p.Key, p => p.Value.AsString()),
                VcsUrl = this.VcsUrl,
                VcsRevision = this.VcsRevision
            };

            if (!string.IsNullOrEmpty(this.BuildAgentName))
            {
                info.BuildAgent = new BuildAgent
                {
                    Name = this.BuildAgentName,
                    Version = this.BuildAgentVersion
                };
            }

            info.ExecutionUrl = $"{SDK.BaseUrl}/executions/execution-details?executionId={context.ExecutionId}";

            /*
            using (var db = new DB.Context())
            {
                //info.Started = new DateTimeOffset((await db.Executions_GetExecutionAsync(context.ExecutionId).ConfigureAwait(false)).Start_Date, TimeSpan.Zero);

                if (this.IncludeIssues)
                {
                    var release = (await db.Releases_GetReleaseAsync(context.ApplicationId, context.ReleaseNumber).ConfigureAwait(false)).Releases_Extended.Single();
                    var sources = await db.IssueSources_GetIssueSourcesAsync(release.Release_Id).ConfigureAwait(false);
                    var issues = (await Task.WhenAll(sources.Select(s => IssueSource.Create(s).EnumerateIssuesAsync(new IssueEnumerationContext(this, context, s)))).ConfigureAwait(false)).SelectMany(i => i);
                    var buildIssues = issues.Select(i => new BuildIssue { Key = i.Id, Url = i.Url, Summary = i.Title });

                    if (sources.Any())
                    {
                        info.Issues = new BuildIssues
                        {
                            Tracker = new BuildAgent
                            {
                                Name = string.Join(", ", sources.Select(s => IssueSource.Create(s).GetDescription().ToString())),
                                Version = "0.0.0"
                            },
                            AffectedIssues = buildIssues,
                        };
                    }
                }
            }
            */

            info.Modules = this.Modules?.Select(m => BuildModule.FromRuntimeValue(this, m.AsDictionary()));

            using (var client = this.CreateClient())
            using (var request = new StringContent(JsonConvert.SerializeObject(info), InedoLib.UTF8Encoding, "application/json"))
            using (var response = await client.PutAsync("api/build", request, context.CancellationToken).ConfigureAwait(false))
            {
                await this.ParseResponseAsync(response).ConfigureAwait(false);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Upload build ", new Hilite(config[nameof(BuildName)]), " ", new Hilite(config[nameof(BuildVersion)]), " (", new Hilite(config[nameof(BuildNumber)]), ")")
            );
        }

        public enum BuildType
        {
            GENERIC,
            MAVEN,
            GRADLE,
            ANT,
            IVY
        }

        private struct BuildInfo
        {
            [JsonProperty(PropertyName = "properties", NullValueHandling = NullValueHandling.Ignore)]
            public IReadOnlyDictionary<string, string> Properties { get; set; }
            [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
            public string Version { get; set; }
            [JsonProperty(PropertyName = "name", Required = Required.Always)]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "number", Required = Required.Always)]
            public string Number { get; set; }
            [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
            public BuildType Type { get; set; }
            [JsonProperty(PropertyName = "buildAgent", NullValueHandling = NullValueHandling.Ignore)]
            public BuildAgent? BuildAgent { get; set; }
            [JsonProperty(PropertyName = "agent", Required = Required.Always)]
            public BuildAgent Agent => new BuildAgent { Name = typeof(UploadBuildOperation).Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product, Version = typeof(Operation).Assembly.GetName().Version.ToString() };
            [JsonProperty(PropertyName = "started", NullValueHandling = NullValueHandling.Ignore)]
            public string StartDate => this.Started?.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'+0000'");
            public DateTimeOffset? Started { get; set; }
            [JsonProperty(PropertyName = "artifactoryPluginVersion", Required = Required.Always)]
            public string ArtifactoryPluginVersion => typeof(UploadBuildOperation).Assembly.GetName().Version.ToString();
            [JsonProperty(PropertyName = "durationMillis", NullValueHandling = NullValueHandling.Ignore)]
            public double? DurationMillis => this.Started.HasValue ? (double?)(DateTimeOffset.Now - this.Started.Value).TotalMilliseconds : null;
            [JsonProperty(PropertyName = "artifactoryPrincipal", Required = Required.Always)]
            public string ArtifactoryPrincipal { get; set; }
            [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
            public string ExecutionUrl { get; set; }
            [JsonProperty(PropertyName = "vcsRevision", NullValueHandling = NullValueHandling.Ignore)]
            public string VcsRevision { get; set; }
            [JsonProperty(PropertyName = "vcsUrl", NullValueHandling = NullValueHandling.Ignore)]
            public string VcsUrl { get; set; }
            [JsonProperty(PropertyName = "modules", NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<BuildModule> Modules { get; set; }
            [JsonProperty(PropertyName = "issues", NullValueHandling = NullValueHandling.Ignore)]
            public BuildIssues? Issues { get; set; }
        }

        private struct BuildAgent
        {
            [JsonProperty(PropertyName = "name", Required = Required.Always)]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "version", Required = Required.Always)]
            public string Version { get; set; }
        }

        private struct BuildModule
        {
            [JsonProperty(PropertyName = "properties", NullValueHandling = NullValueHandling.Ignore)]
            public IReadOnlyDictionary<string, string> Properties { get; set; }
            [JsonProperty(PropertyName = "id", Required = Required.Always)]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "artifacts", NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<BuildArtifact> Artifacts { get; set; }
            [JsonProperty(PropertyName = "dependencies", NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<BuildDependency> Dependencies { get; set; }

            internal static BuildModule FromRuntimeValue(ILogSink logger, IDictionary<string, RuntimeValue> value)
            {
                if (!value.ContainsKey("id"))
                {
                    logger.LogWarning("Build module missing \"id\" field.");
                }

                foreach (var key in value.Keys)
                {
                    if (key != "id" && key != "properties" && key != "artifacts" && key != "dependencies")
                    {
                        logger.LogWarning($"Build module has unknown field \"{key}\".");
                    }
                }

                return new BuildModule
                {
                    Id = value["id"].AsString(),
                    Properties = value.ContainsKey("properties") ? value["properties"].AsDictionary().ToDictionary(p => p.Key, p => p.Value.AsString()) : null,
                    Artifacts = value.ContainsKey("artifacts") ? value["artifacts"].AsEnumerable().Select(a => BuildArtifact.FromRuntimeValue(logger, a.AsDictionary())) : null,
                    Dependencies = value.ContainsKey("dependencies") ? value["dependencies"].AsEnumerable().Select(d => BuildDependency.FromRuntimeValue(logger, d.AsDictionary())) : null
                };
            }
        }

        private struct BuildArtifact
        {
            [JsonProperty(PropertyName = "type", Required = Required.Always)]
            public string Type { get; set; }
            [JsonProperty(PropertyName = "sha1", Required = Required.Always)]
            public string Sha1 { get; set; }
            [JsonProperty(PropertyName = "md5", Required = Required.Always)]
            public string Md5 { get; set; }
            [JsonProperty(PropertyName = "name", Required = Required.Always)]
            public string Name { get; set; }

            internal static BuildArtifact FromRuntimeValue(ILogSink logger, IDictionary<string, RuntimeValue> value)
            {
                return new BuildArtifact
                {
                    Type = value["type"].AsString(),
                    Sha1 = value["sha1"].AsString(),
                    Md5 = value["md5"].AsString(),
                    Name = value["name"].AsString()
                };
            }
        }

        private struct BuildDependency
        {
            [JsonProperty(PropertyName = "type", Required = Required.Always)]
            public string Type { get; set; }
            [JsonProperty(PropertyName = "sha1", Required = Required.Always)]
            public string Sha1 { get; set; }
            [JsonProperty(PropertyName = "md5", Required = Required.Always)]
            public string Md5 { get; set; }
            [JsonProperty(PropertyName = "id", Required = Required.Always)]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "scopes", Required = Required.Always)]
            public IEnumerable<string> Scopes { get; set; }

            internal static BuildDependency FromRuntimeValue(ILogSink logger, IDictionary<string, RuntimeValue> value)
            {
                return new BuildDependency
                {
                    Type = value["type"].AsString(),
                    Sha1 = value["sha1"].AsString(),
                    Md5 = value["md5"].AsString(),
                    Id = value["id"].AsString(),
                    Scopes = value["scopes"].AsEnumerable().Select(s => s.AsString())
                };
            }
        }

        private struct BuildIssues
        {
            [JsonProperty(PropertyName = "tracker", Required = Required.Always)]
            public BuildAgent Tracker { get; set; }
            [JsonProperty(PropertyName = "affectedIssues", Required = Required.Always)]
            public IEnumerable<BuildIssue> AffectedIssues { get; set; }
        }

        private struct BuildIssue
        {
            [JsonProperty(PropertyName = "key", Required = Required.Always)]
            public string Key { get; set; }
            [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
            public string Url { get; set; }
            [JsonProperty(PropertyName = "summary", Required = Required.Always)]
            public string Summary { get; set; }
        }

        /*
        private class IssueEnumerationContext : IIssueSourceEnumerationContext
        {
            public IssueEnumerationContext(UploadBuildOperation operation, IOperationExecutionContext context, Tables.IssueSources_Extended data)
            {
                this.IssueSourceId = data.IssueSource_Id;
                this.ExecutionId = context.ExecutionId;
                this.Log = operation;
            }

            public int? IssueSourceId { get; }
            public int? ExecutionId { get; }
            public ILogger Log { get; }
        }
        */
    }
}
