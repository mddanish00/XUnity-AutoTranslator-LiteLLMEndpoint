using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using SimpleJSON;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using XUnity.Common.Logging;

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
        private string[] ExtraArguments { get; set; }
        private string ConfigPath { get; set; }
        private string ApiKey { get; set; }
        private string ModelName { get; set; }
        private float ModelTemperature { get; set; }
        private float ModelTopK { get; set; }
        private float ModelFrequencyPenalty { get; set; }
        private float ModelPresencePenalty { get; set; }
        private bool UseExternalServer { get; set; }
        private bool LogServerMessages { get; set; }

        public override void Initialize(IInitializationContext context)
        {
            // Config
            this.ServerPort = context.GetOrCreateSetting("LiteLLM", "ServerPort", "4000");
            this.BinaryPath = context.GetOrCreateSetting("LiteLLM", "BinaryPath", "");
            this.ExtraArguments = context.GetOrCreateSetting<string[]>("LiteLLM", "ExtraArguments", new string[0]);
            this.ConfigPath = context.GetOrCreateSetting("LiteLLM", "ConfigPath", "");
            this.ApiKey = context.GetOrCreateSetting("LiteLLM", "ApiKey", "");
            this.ModelName = context.GetOrCreateSetting("LiteLLM", "ModelName", "");
            this.ModelTemperature = context.GetOrCreateSetting("LiteLLM", "Temperature", 0.2f);
            this.ModelTopK = context.GetOrCreateSetting("LiteLLM", "TopK", 0.9f);
            this.ModelFrequencyPenalty = context.GetOrCreateSetting("LiteLLM", "FrequencyPenalty", 0f);
            this.ModelPresencePenalty = context.GetOrCreateSetting("LiteLLM", "PresencePenalty", 0f);
            this.UseExternalServer = context.GetOrCreateSetting("LiteLLM", "UseExternalServer", false);
            this.LogServerMessages = context.GetOrCreateSetting("LiteLLM", "LogServerMessages", false);

            // Remove artificial delays
            context.SetTranslationDelay(0.1f);
            context.DisableSpamChecks();

            if ((string.IsNullOrEmpty(this.BinaryPath) || string.IsNullOrEmpty(this.ConfigPath)) && !this.UseExternalServer)
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
            if (configuredEndpoint == this.Id && !this.UseExternalServer)
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
            var rawText = context.UntranslatedText;
            foreach (var glossaryLine in Configuration.config.GlossaryLines)
            {
                if (glossaryLine.SubstituteBeforeTranslate && rawText.Contains(glossaryLine.Raw))
                {
                    rawText = rawText.Replace(glossaryLine.Raw, glossaryLine.Result);
                }
            }

            var requestData = Factory.GetRequestData(rawText);
            var request = new XUnityWebRequest("POST", $"http://localhost:{this.ServerPort}/v1/chat/completions", requestData);
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
            if (this.process == null || this.process.HasExited)
            {
                XuaLogger.AutoTranslator.Info($"Running LiteLLM:\n\tBinaryPath: {this.BinaryPath}");

                string[] argumentsList = new string[]
                {
                    "--port",
                    this.ServerPort,
                    "--config",
                    this.ConfigPath
                };
                argumentsList = argumentsList.Concat(this.ExtraArguments).ToArray();

                this.process = new Process();
                this.process.StartInfo = new ProcessStartInfo()
                {
                    FileName = this.BinaryPath,
                    Arguments = string.Join(" ", argumentsList.ToArray()),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                };

                this.process.OutputDataReceived += this.ServerDataReceivedEventHandler;
                this.process.ErrorDataReceived += this.ServerDataReceivedEventHandler;

                this.process.Start();
                this.process.BeginErrorReadLine();
                this.process.BeginOutputReadLine();
                this.isStarted = true;
            }
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

        void ServerDataReceivedEventHandler(object sender, DataReceivedEventArgs args)
        {
            if (this.LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info(args.Data);
            }

            if (!this.isReady && args.Data.Contains("INFO"))
            {
                this.isReady = true;
                XuaLogger.AutoTranslator.Info("Application startup complete.");
            }
        }

        IEnumerator ITranslateEndpoint.Translate(ITranslationContext context)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var iterator = base.Translate(context);

            while (iterator.MoveNext()) yield return iterator.Current;

            var elapsed = stopwatch.Elapsed.TotalSeconds;

            if (this.LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info($"Translate complete {elapsed}s");
            }
        }

        public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
        {
            if (this.isStarted && this.process.HasExited)
            {
                this.isStarted = false;
                this.isReady = false;

                XuaLogger.AutoTranslator.Warn($"Translator server process exited unexpectedly [status {process.ExitCode}]");
            }

            if (!this.isStarted && !this.isDisposing)
            {
                XuaLogger.AutoTranslator.Warn($"Translator server process not running. Starting...");
                this.StartProcess();
            }

            while (!this.isReady) yield return null;
        }

    }
}