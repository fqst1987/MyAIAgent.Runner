using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyAIAgent.Runner.Agents
{
    public class DbContextAgent
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public DbContextAgent(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        public async Task<string> GenerateDbContextAsync(string metadataJson, string classesInfo)
        {
            var systemPrompt = @"
You are a Senior C# Entity Framework Core Architect.
Your task is to generate the complete `AppDbContext.cs` code containing the `AppDbContext` and its factory, based on the provided JSON Metadata of all tables and the generated POCO class names.

Core Constraints & Rules:
1. Include required namespaces like `using Microsoft.EntityFrameworkCore;`, `using Microsoft.EntityFrameworkCore.Design;`.
2. Generate an `AppDbContextFactory` implementing `IDesignTimeDbContextFactory<AppDbContext>` using SQLite (`optionsBuilder.UseSqlite(""Data Source=app.db"");`).
3. Generate the `AppDbContext` inheriting from `DbContext`, with the necessary constructors and the `OnConfiguring` method (using SQLite with `Data Source=app.db`).
4. Generate `DbSet<T>` for each entity provided.
5. EXTREMELY IMPORTANT: Implement `OnModelCreating(ModelBuilder modelBuilder)` to configure ALL foreign key relationships using Fluent API based on the `RelatedTables` inside the provided JSON metadata.
6. CAREFULLY handle Alternate Keys: If a Foreign Key targets a property that is NOT the Primary Key (e.g. string matching string, but PK is int), you MUST use `.HasPrincipalKey(...)` in your Fluent API configuration (e.g., `.HasPrincipalKey(p => p.TargetAlternateKeyProperty)`).
7. ONLY return the final C# code. Do not output markdown code blocks (```csharp), explanations, or thought process.
";

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
                            new { text = $"Here is the JSON Metadata of all tables:\n{metadataJson}\n\nHere are the generated DbSets information:\n{classesInfo}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Call Failed in DbContextAgent: {response.StatusCode}\n{errorBody}");
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
                throw new Exception("Failed to parse the Gemini response format in DbContextAgent.", ex);
            }

            return string.Empty;
        }
    }
}
