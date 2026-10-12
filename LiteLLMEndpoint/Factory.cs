using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SimpleJSON;

namespace LiteLLM
{
    public static class Factory
    {
        public static string GetRequestData(string raw)
        {
            var systemPrompt = new StringBuilder(Configuration.config.SystemPrompt);
            systemPrompt.AppendLine(ConstructGlossaryPrompt(raw));

            // Create messages using SimpleJSON (since some games don't have access to Newtonsoft)
            var messages = new JSONArray();
            var systemMessage = new JSONObject();
            systemMessage["role"] = "system";
            systemMessage["content"] = systemPrompt.ToString();
            messages.Add(systemMessage);

            var userMessage = new JSONObject();
            userMessage["role"] = "user";
            userMessage["content"] = raw;
            messages.Add(userMessage);

            // Create the requestBody object using SimpleJSON
            var requestBody = new JSONObject
            {
                ["model"] = Configuration.modelConfig.Model,
                ["stream"] = false,
                ["messages"] = messages
            };

            foreach (var param in Configuration.modelConfig.ModelParams)
            {
                if (decimal.TryParse(param.Value.ToString(), out decimal isDecimal))
                    requestBody[param.Key] = (double)isDecimal;
                else if (int.TryParse(param.Value.ToString(), out int isInt))
                    requestBody[param.Key] = isInt;
                else
                    requestBody[param.Key] = param.Value.ToString();
            }

            return requestBody.ToString();
        }

        public static string ConstructGlossaryPrompt(string raw)
        {
            var prompt = new StringBuilder(Configuration.config.GlossaryPrompt);
            prompt.AppendLine(AppendPromptsFor(raw, Configuration.config.GlossaryLines));
            return prompt.ToString();
        }

        private static string AppendPromptsFor(string raw, List<GlossaryLine> glossaryLines)
        {
            var prompt = new StringBuilder();

            foreach (var line in glossaryLines)
            {
                if (raw.Contains(line.Raw))
                    prompt.Append($"- {line.Raw}: {line.Result}\n");
            }

            return prompt.ToString();
        }

        public static string ValidateAndCleanupTranslation(string raw, string result)
        {
            // Check glossary mistranslation here
            // If we do any other clean up should be done here
            if ((result.StartsWith("\"") && result.EndsWith("\""))
                || (result.StartsWith("'") && result.EndsWith("'")))
                result = result.Substring(1, result.Length - 2);

            //Take out wide quotes
            result = result
                .Replace("’", "'")
                .Replace("‘", "'");

            result = Regex.Unescape(result);

            //Make sure first character is upper case
            if (Char.IsLower(result[0]) && raw != result)
                result = Char.ToUpper(result[0]) + result.Substring(1, result.Length - 1);

            return result.Trim();
        }
    }
}