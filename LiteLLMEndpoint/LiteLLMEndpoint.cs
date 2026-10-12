using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using SimpleJSON;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;

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
        private float ModelTemperature { get; set; }
        private float ModelTopK { get; set; }
        private float ModelFrequencyPenalty { get; set; }
        private float ModelPresencePenalty { get; set; }
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
            this.ModelTemperature = context.GetOrCreateSetting("LiteLLM", "Temperature", 0.2f);
            this.ModelTopK = context.GetOrCreateSetting("LiteLLM", "TopK", 0.9f);
            this.ModelFrequencyPenalty = context.GetOrCreateSetting("LiteLLM", "FrequencyPenalty", 0f);
            this.ModelPresencePenalty = context.GetOrCreateSetting("LiteLLM", "PresencePenalty", 0f);
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

            // Load configuration
            var configFolderPath = $"{Configuration.CalculateConfigFolder(context.TranslatorDirectory)}/LiteLLM";
            Configuration.LoadConfiguration(configFolderPath);
            Configuration.LoadModelConfig(this.ModelName,
            new KeyValuePair<string, object>("temperature", this.ModelTemperature),
            new KeyValuePair<string, object>("top_p", this.ModelTopK),
            new KeyValuePair<string, object>("frequency_penalty", this.ModelFrequencyPenalty),
            new KeyValuePair<string, object>("presence_penalty", this.ModelPresencePenalty));
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var requestData = Factory.GetRequestData(context.UntranslatedText);
            var request = new XUnityWebRequest("POST", $"http://127.0.0.1:{this.ServerPort}/", requestData);
            request.Headers[HttpRequestHeader.Authorization] = $"Bearer {this.ApiKey}";
            request.Headers[HttpRequestHeader.ContentType] = "application/json";

            context.Complete(request);
        }

        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            var data = context.Response.Data;

            var jsonResponse = JSON.Parse(data);
            var result = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;
            result = Factory.ValidateAndCleanupTranslation(context.UntranslatedText, result);

            if (MaxTranslationsPerRequest == 1)
                context.Complete(result);
        }

        private void StartProcess()
        {

        }

        public void Dispose()
        {
            this.isDisposing = true;
            if (this.process != null)
            {
                this.process.Kill();
                this.process.Dispose();
                this.process = null;
            }
        }

    }
}