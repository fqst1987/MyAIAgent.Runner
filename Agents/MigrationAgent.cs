using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyAIAgent.Runner.Agents
{
    public interface IMigrationAgent
    {
        Task<string> AnalyzeMigrationSafetyAsync(string migrationCode);
    }

    public class MigrationAgent : IMigrationAgent
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public MigrationAgent(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        public async Task<string> AnalyzeMigrationSafetyAsync(string migrationCode)
        {
            var systemPrompt = @"
You are an expert DBA and Entity Framework Core Migration Agent.
Your job is to read the provided EF Core Migration C# script (specifically the `Up` method) and determine if applying it is completely safe or if it risks DATA LOSS.

Safety Rules:
1. Operations like `migrationBuilder.CreateTable`, `AddColumn`, `CreateIndex`, `AddForeignKey` are SAFE.
2. Operations like `migrationBuilder.DropTable`, `DropColumn` are UNSAFE because they destroy existing data.
3. Operations like altering a column type in a destructive way (e.g. from string to int without conversion) are UNSAFE.

If the migration is completely safe, return exactly the word: SAFE
If the migration contains unsafe operations, start your response with UNSAFE followed by a brief, polite explanation of the risk. Do not output markdown or extra text if it is SAFE.
";

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = $"Please evaluate this migration:\n\n{migrationCode}" } } } },
                generationConfig = new { temperature = 0.1 }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini API Call Failed in Role C (Migration Agent): {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var resultObject = JsonDocument.Parse(responseBody);
            
            try 
            {
                var generatedText = resultObject.RootElement
                                                .GetProperty("candidates")[0]
                                                .GetProperty("content")
                                                .GetProperty("parts")[0]
                                                .GetProperty("text").GetString();

                return generatedText?.Trim() ?? "UNKNOWN";
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse the Gemini response format in Role C.", ex);
            }
        }
    }
}
