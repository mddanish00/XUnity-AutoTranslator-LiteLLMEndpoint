using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LiteLLM
{
    public class AIConfig
    {
        public string SystemPrompt { get; set; } = string.Empty;
        public string GlossaryPrompt { get; set; } = string.Empty;
        public List<GlossaryLine> GlossaryLines { get; set; } = new List<GlossaryLine>();
    }

    public class ModelConfig
    {
        public string Model { get; set; } = string.Empty;
        public Dictionary<string, object> ModelParams { get; set; } = new Dictionary<string, object>();
    }

    public class GlossaryLine
    {
        public string Raw { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Transliteration { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public bool SubstituteBeforeTranslate { get; set; } = false;
        public bool CheckForHallucination { get; set; } = true;
        public bool CheckForMistranslation { get; set; } = true;

        public GlossaryLine()
        {
        }

        public GlossaryLine(string raw, string result, string context)
        {
            Raw = raw;
            Result = result;
            Context = context ?? string.Empty;
        }
    }

    public static class Configuration
    {
        public static readonly AIConfig config = new AIConfig();
        public static readonly ModelConfig modelConfig = new ModelConfig();

        public static string CalculateConfigFolder(string translatorDirectoryInput = null)
        {
            var translatorDirectory = translatorDirectoryInput ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(translatorDirectory))
                throw new InvalidOperationException("Unable to determine the translator directory.");

            var directory = new DirectoryInfo(Path.GetFullPath(translatorDirectory));
            DirectoryInfo unityDataDirectory = null;

            for (var current = directory; current != null; current = current.Parent)
            {
                if (string.Equals(current.Name, "BepInEx", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(current.FullName, "config");

                if (unityDataDirectory == null &&
                    (current.Name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase) ||
                     current.Name.EndsWith("_ManagedData", StringComparison.OrdinalIgnoreCase)))
                {
                    unityDataDirectory = current;
                }
            }

            if (unityDataDirectory?.Parent != null)
                return Path.Combine(unityDataDirectory.Parent.FullName, "AutoTranslator");

            // Preserve the legacy ReiPatcher layout fallback:
            // <Game>/<Game>_Data/Managed/Translators -> <Game>/AutoTranslator.
            return Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(directory.FullName, ".."), Path.Combine("..", "..")), "AutoTranslator"));
        }

        public static void LoadConfiguration(string configFolder)
        {
            // Check if the prompt files and glossary file exist
            if (!File.Exists($"{configFolder}/SystemPrompt.txt"))
                throw new Exception($"Missing System Prompt File: {configFolder}/SystemPrompt.txt");
            if (!File.Exists($"{configFolder}/GlossaryPrompt.txt"))
                throw new Exception($"Missing Glossary Prompt File: {configFolder}/GlossaryPrompt.txt");
            if (!File.Exists($"{configFolder}/Glossary.yaml"))
                throw new Exception($"Missing Glossary File: {configFolder}/Glossary.yaml");

            config.SystemPrompt = File.ReadAllText($"{configFolder}/SystemPrompt.txt", Encoding.UTF8);
            config.GlossaryPrompt = File.ReadAllText($"{configFolder}/GlossaryPrompt.txt", Encoding.UTF8);

            if (string.IsNullOrEmpty(config.GlossaryPrompt))
                config.GlossaryPrompt = "### Glossary for Consistent Translations\r\nPrioritise and use the translation when an exact match with the original text is found.\r\n## Terms";

            // Load Glossary file from yaml
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();
            config.GlossaryLines = yamlDeserializer.Deserialize<List<GlossaryLine>>(File.ReadAllText($"{configFolder}/Glossary.yaml", Encoding.UTF8));
        }

        public static void LoadModelConfig(string modelName, params KeyValuePair<string, object>[] modelParams)
        {
            modelConfig.Model = modelName;
            foreach (var kvp in modelParams)
            {
                modelConfig.ModelParams[kvp.Key] = kvp.Value;
            }

        }
    }
}