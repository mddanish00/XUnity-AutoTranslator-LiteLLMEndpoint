/*
MIT License

Copyright (c) 2026 mddanish00

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Collections;
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
        private string[] ModelParams { get; set; }
        private bool UseExternalServer { get; set; }
        private bool LogServerMessages { get; set; }

        public override void Initialize(IInitializationContext context)
        {
            // Config
            ServerPort = context.GetOrCreateSetting("LiteLLM", "ServerPort", "4000");
            BinaryPath = context.GetOrCreateSetting("LiteLLM", "BinaryPath", "");
            ExtraArguments = Factory.GetIniArray(context.GetOrCreateSetting("LiteLLM", "ExtraArguments", ""));
            ConfigPath = context.GetOrCreateSetting("LiteLLM", "ConfigPath", "");
            ApiKey = context.GetOrCreateSetting("LiteLLM", "ApiKey", "");
            ModelName = context.GetOrCreateSetting("LiteLLM", "ModelName", "");
            ModelParams = Factory.GetIniArray(context.GetOrCreateSetting("LiteLLM", "ModelParams", "temperature=0.2;top_p=0.9;frequency_penalty=0;presence_penalty=0"));
            UseExternalServer = context.GetOrCreateSetting("LiteLLM", "UseExternalServer", false);
            LogServerMessages = context.GetOrCreateSetting("LiteLLM", "LogServerMessages", false);

            // Remove artificial delays
            context.SetTranslationDelay(0.1f);
            context.DisableSpamChecks();

            if ((string.IsNullOrEmpty(BinaryPath) || string.IsNullOrEmpty(ConfigPath)) && !UseExternalServer)
            {
                throw new Exception($"LiteLLMEndpoint: BinaryPath or ConfigPath is not set. Please configure them in the settings.");
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new Exception($"LiteLLMEndpoint: ApiKey is not set. Please configure your Master Key or Virtual Key in the settings.");
            }

            if (string.IsNullOrEmpty(ModelName))
            {
                throw new Exception($"LiteLLMEndpoint: ModelName is not set. Please configure your model name in the settings.");
            }

            var configuredEndpoint = context.GetOrCreateSetting<string>("Service", "Endpoint");
            // Only start the process if this endpoint is the configured one
            if (configuredEndpoint == Id && !UseExternalServer)
            {
                StartProcess();
            }

            // Load configuration
            var configFolderPath = $"{Configuration.CalculateConfigFolder(context.TranslatorDirectory)}/LiteLLM";
            Configuration.LoadConfiguration(configFolderPath);
            Configuration.LoadModelConfig(ModelName, ModelParams);
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
            var request = new XUnityWebRequest("POST", $"http://127.0.0.1:{ServerPort}/v1/chat/completions", requestData);
            request.Headers[HttpRequestHeader.Authorization] = $"Bearer {ApiKey}";
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
            if (process == null || process.HasExited)
            {
                XuaLogger.AutoTranslator.Info($"Running LiteLLM:\n\tBinaryPath: {BinaryPath}");

                string[] argumentsList = new string[]
                {
                    "--port",
                    ServerPort,
                    "--config",
                    ConfigPath
                };

                // Flatten the ExtraArguments array and split by '=' to handle key-value pairs
                var processExtraArguments = ExtraArguments.SelectMany(arg =>
                {
                    var splitArr = arg.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    splitArr[0] = $"--{splitArr[0]}"; // Prefix with '--'
                    return splitArr;
                }).ToArray();
                argumentsList = argumentsList.Concat(processExtraArguments).ToArray();

                process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = BinaryPath,
                        Arguments = string.Join(" ", argumentsList.ToArray()),
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                    }
                };

                process.OutputDataReceived += ServerDataReceivedEventHandler;
                process.ErrorDataReceived += ServerDataReceivedEventHandler;

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                isStarted = true;
            }
        }

        public void Dispose()
        {
            isDisposing = true;
            if (process != null)
            {
                process.Kill();
                process.Dispose();
                process = null;
            }
        }

        void ServerDataReceivedEventHandler(object sender, DataReceivedEventArgs args)
        {
            if (LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info(args.Data);
            }

            if (!isReady && args.Data.Contains("INFO"))
            {
                isReady = true;
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

            if (LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info($"Translate complete {elapsed}s");
            }
        }

        public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
        {
            if (UseExternalServer)
            {
                yield break;
            }

            if (isStarted && process.HasExited)
            {
                isStarted = false;
                isReady = false;

                XuaLogger.AutoTranslator.Warn($"Translator server process exited unexpectedly [status {process.ExitCode}]");
            }

            if (!isStarted && !isDisposing)
            {
                XuaLogger.AutoTranslator.Warn($"Translator server process not running. Starting...");
                StartProcess();
            }

            while (!isReady) yield return null;
        }

    }
}