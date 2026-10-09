using System;
using System.Diagnostics;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;

namespace LiteLLM
{
    public class LiteLLMEndpoint : HttpEndpoint, ITranslateEndpoint, IDisposable
    {
        public override string Id => "LiteLLM";
        public override string FriendlyName => "LiteLLM";
        public override int MaxTranslationsPerRequest => 1;
        public override int MaxConcurrency => 15;

        private Process process;
        private bool isDisposing = false;
        private bool isStarted = false;
        private bool isReady = false;

        private string ServerPort { get; set; }
        private string BinaryPath { get; set; }
        private Array AddArguments { get; set; }
        private string ConfigPath { get; set; }
        private string ApiKey { get; set; }
        private string ModelName { get; set; }
        private bool LogServerMessages { get; set; }

        public override void Initialize(IInitializationContext context)
        {
            // Config
            this.ServerPort = context.GetOrCreateSetting("LiteLLM", "ServerPort", "4000");
            this.BinaryPath = context.GetOrCreateSetting("LiteLLM", "BinaryPath", "");
            this.AddArguments = context.GetOrCreateSetting("LiteLLM", "AddArguments", new string[] { });
            this.ConfigPath = context.GetOrCreateSetting("LiteLLM", "ConfigPath", "");
            this.ApiKey = context.GetOrCreateSetting("LiteLLM", "ApiKey", "");
            this.ModelName = context.GetOrCreateSetting("LiteLLM", "ModelName", "");
            this.LogServerMessages = context.GetOrCreateSetting("LiteLLM", "LogServerMessages", false);

            // Remove artificial delays
            context.SetTranslationDelay(0.1f);
            context.DisableSpamChecks();

            if (string.IsNullOrEmpty(this.BinaryPath) || string.IsNullOrEmpty(this.ConfigPath))
            {
                throw new Exception($"LiteLLMEndpoint: BinaryPath or ConfigPath is not set. Please configure them in the settings.");
            }

            if (string.IsNullOrEmpty(this.ApiKey))
            {
                throw new Exception($"LiteLLMEndpoint: ApiKey is not set. Please configure your Master Key or Virtual Key in the settings.");
            }

            if (string.IsNullOrEmpty(this.ModelName))
            {
                throw new Exception($"LiteLLMEndpoint: ModelName is not set. Please configure your model name in the settings.");
            }

            var configuredEndpoint = context.GetOrCreateSetting<string>("Service", "Endpoint");
            // Only start the process if this endpoint is the configured one
            if (configuredEndpoint == this.Id)
            {
                this.StartProcess();
            }
        }

    }