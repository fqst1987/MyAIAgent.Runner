using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyAIAgent.Runner.Agents
{
    public interface ISystemReporterAgent
    {
        Task<string> ReportErrorFriendlyAsync(string technicalErrorMessage);
    }

    public class SystemReporterAgent : ISystemReporterAgent
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public SystemReporterAgent(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        public async Task<string> ReportErrorFriendlyAsync(string technicalErrorMessage)
        {
            var systemPrompt = @"
You are a friendly and professional Assistant System Agent in a multi-agent system.
Your job is to read a raw technical Exception or error message and translate it into a polite, human-friendly response to the developer.
For example, if the error says 'Missing dependency table: X', you should say something like: 'Oops! I noticed we are missing the Excel file for table X. Could you please provide it so we can continue?'
Do not provide coding solutions or C# code. Just explain the error in plain, empathetic Traditional Chinese.
";

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = $"System Error:\n{technicalErrorMessage}" } } } },
                generationConfig = new { temperature = 0.6 }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                return $"[System Fallback] Error: {technicalErrorMessage}";
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var resultObject = JsonDocument.Parse(responseBody);
            
            try 
            {
                return resultObject.RootElement
                                   .GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString()?.Trim() ?? "[Empty Response]";
            }
            catch
            {
                return $"[System Fallback] Error: {technicalErrorMessage}";
            }
        }
    }
}
