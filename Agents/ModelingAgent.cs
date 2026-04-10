using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyAIAgent.Runner.Agents
{
    public interface IModelingAgent
    {
        Task<string> GenerateEntityCodeAsync(string metadataJson);
    }

    public class ModelingAgent : IModelingAgent
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        // Use standard HttpClient for direct REST API access to ensure zero missing-package issues.
        public ModelingAgent(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        public async Task<string> GenerateEntityCodeAsync(string metadataJson)
        {
            var systemPrompt = @"
You are a highly skilled C# Modeling Engineer.
Your task is to read the provided table metadata (in JSON format) and generate a pure C# Entity Class (POCO).

Core constraints:
1. Map SQL data types to standard C# native types (e.g., varchar -> string, int -> int, bit/bool -> bool).
2. Look at 'relatedTables'. Use this to generate standard EF Core navigation properties.
    - If relativeType is '1:N', generate a `public virtual ICollection<ChildType> ChildTypes { get; set; } = new List<ChildType>();`.
    - If there is a foreignKey representing N:1, generate `public virtual ParentType Parent { get; set; }`.
3. Do NOT add ANY Entity Framework Attributes (like [Key], [Table], [Required], [MaxLength] etc). Another agent will handle validation attributes. We only want pure POCOs here.
4. ONLY return the valid C# class code. Do not output markdown code blocks (```csharp), explanations, greetings, or thoughts. Just output the raw text for the .cs file.
";

            // Gemini API JSON payload structure
            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"Here is the JSON Metadata:\n{metadataJson}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2 // keep it deterministic for code generation
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Call Failed: {response.StatusCode}\n{errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var resultObject = JsonDocument.Parse(responseBody);
            
            try 
            {
                // Extract the generated text from Gemini Response: candidates[0].content.parts[0].text
                var generatedText = resultObject.RootElement
                                                .GetProperty("candidates")[0]
                                                .GetProperty("content")
                                                .GetProperty("parts")[0]
                                                .GetProperty("text").GetString();

                // Clean up markdown ```csharp formatting if Gemini ignores instructions
                if (generatedText != null)
                {
                    generatedText = generatedText.Trim();
                    if (generatedText.StartsWith("```csharp", StringComparison.OrdinalIgnoreCase))
                    {
                        generatedText = generatedText.Substring(9);
                    }
                    else if (generatedText.StartsWith("```cs", StringComparison.OrdinalIgnoreCase))
                    {
                        generatedText = generatedText.Substring(5);
                    }
                    else if (generatedText.StartsWith("```"))
                    {
                        generatedText = generatedText.Substring(3);
                    }

                    if (generatedText.EndsWith("```"))
                    {
                        generatedText = generatedText.Substring(0, generatedText.Length - 3);
                    }

                    return generatedText.Trim();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse the Gemini response format.", ex);
            }

            return string.Empty;
        }
    }
}
