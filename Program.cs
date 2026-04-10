using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using MyAIAgent.Runner.Helper;
using MyAIAgent.Runner.Agents;

namespace MyAIAgent.Runner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("SKIP_ORCHESTRATOR") == "1" || 
                args.Any(a => a.Contains("ef") || a.Contains("applicationName")))
            {
                return; // EF Core Design-time execution bypass
            }

            // Ensure the working directory is the project root, not bin/Debug or similar
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (currentDir != null)
            {
                if (currentDir.GetFiles("*.csproj").Any())
                {
                    Environment.CurrentDirectory = currentDir.FullName;
                    break;
                }
                currentDir = currentDir.Parent;
            }

            Console.WriteLine("=== [Role D] Excel Architect: Extracting Metadata ===");
            var helper = new ExcelHelper();
            
            // Ensures directory exists
            if (!System.IO.Directory.Exists("Datas"))
            {
                System.IO.Directory.CreateDirectory("Datas");
            }
            
            var jsonMetadata = await helper.ReadDirectoryAsync("Datas");
            Console.WriteLine("Extraction Complete. Forwarding to Role A.");
            Console.WriteLine();

            // =========================================================
            // ACTION REQUIRED: Paste your Gemini API Key here
            // =========================================================
            string geminiApiKey = "AIzaSyBzOf7UYmEDnuS0UZy5gWnlL9Ea7t16U2I";
            
            if (geminiApiKey == "YOUR_GEMINI_API_KEY_HERE")
            {
                Console.WriteLine("!!! PLEASE REPLACE 'YOUR_GEMINI_API_KEY_HERE' IN Program.cs WITH YOUR ACTUAL KEY !!!");
                return;
            }

            
            try 
            {
                Console.WriteLine("=== [Orchestrator] Checking Global Dependencies ===");
                var rootArray = JsonDocument.Parse(jsonMetadata).RootElement;
                
                // 1. Collect all known tables from the batch
                var knownUploadedTables = new List<string>();
                foreach (var tableJson in rootArray.EnumerateArray())
                {
                    knownUploadedTables.Add(tableJson.GetProperty("tableName").GetString() ?? string.Empty);
                }

                // 2. Cross-check all dependencies
                var missingDependencies = new List<string>();
                foreach (var tableJson in rootArray.EnumerateArray())
                {
                    if (tableJson.TryGetProperty("relatedTables", out var relTables))
                    {
                        foreach (var relTable in relTables.EnumerateArray())
                        {
                            var requiredTable = relTable.GetProperty("relativeTable").GetString();
                            if (!string.IsNullOrEmpty(requiredTable) && !knownUploadedTables.Contains(requiredTable))
                            {
                                missingDependencies.Add(requiredTable);
                            }
                        }
                    }
                }

                if (missingDependencies.Any())
                {
                    // Fail fast!
                    throw new InvalidOperationException($"Missing Excel file dependency for relative table(s): {string.Join(", ", missingDependencies.Distinct())}");
                }
                
                Console.WriteLine("Dependencies Validated! Proceeding to Generation.");
                Console.WriteLine();

                // Ensure Models folder exists
                string modelsDir = "Models";
                if (!System.IO.Directory.Exists(modelsDir))
                {
                    System.IO.Directory.CreateDirectory(modelsDir);
                }

                var modelingAgent = new ModelingAgent(geminiApiKey);
                var reviewerAgent = new ModelingReviewerAgent(geminiApiKey);

                var generatedClassNames = new List<(string ClassName, string TableName)>();

                // 3. Process each table
                foreach (var tableJson in rootArray.EnumerateArray())
                {
                    var tableName = tableJson.GetProperty("tableName").GetString() ?? "Unknown";
                    string singleTableJson = tableJson.GetRawText();

                    Console.WriteLine($"--- Processing Table: {tableName} ---");
                    
                    Console.WriteLine($"[Role A] Generating POCO for {tableName}...");
                    var roleACode = await modelingAgent.GenerateEntityCodeAsync(singleTableJson);
                    
                    Console.WriteLine($"[Role B] Decorating Entity for {tableName}...");
                    var finalCode = await reviewerAgent.ReviewAndDecorateEntityAsync(singleTableJson, roleACode);

                    // 4. Save to Models folder
                    string fileName = $"{tableName}.cs";
                    string extractedClassName = tableName;
                    var match = System.Text.RegularExpressions.Regex.Match(finalCode, @"public\s+class\s+([a-zA-Z0-9_]+)");
                    if (match.Success)
                    {
                        extractedClassName = match.Groups[1].Value;
                        fileName = $"{extractedClassName}.cs";
                    }

                    generatedClassNames.Add((extractedClassName, tableName));

                    string filePath = System.IO.Path.Combine(modelsDir, fileName);
                    System.IO.File.WriteAllText(filePath, finalCode, System.Text.Encoding.UTF8);
                    
                    Console.WriteLine($"[Complete] Saved to {filePath}");
                    Console.WriteLine();
                }
                
                Console.WriteLine("All files successfully generated and saved!");
                Console.WriteLine();

                // =========================================================
                // [Role C] Migration Agent Workflow
                // =========================================================
                Console.WriteLine("=== [Orchestrator] Generating AppDbContext ===");
                
                var dbContextAgent = new DbContextAgent(geminiApiKey);
                var classesInfo = JsonSerializer.Serialize(generatedClassNames.Select(x => new { ClassName = x.ClassName, TableName = x.TableName }));
                
                Console.WriteLine("Passing Database Schema to Role E (DbContext Agent) to configure relationships (Fluent API)...");
                var dbContextCode = await dbContextAgent.GenerateDbContextAsync(jsonMetadata, classesInfo);
                
                string dbContextPath = System.IO.Path.Combine(modelsDir, "AppDbContext.cs");
                System.IO.File.WriteAllText(dbContextPath, dbContextCode, System.Text.Encoding.UTF8);
                Console.WriteLine($"[Complete] AppDbContext.cs generated using AI with proper Foreign Key/Alternate Key mappings.");
                Console.WriteLine();

                Console.WriteLine("=== [Role C] Migration Agent: Generating Migration Script ===");
                
                // Invoke dotnet ef to create migration
                string migrationName = $"AutoMigration_{DateTime.Now:yyyyMMddHHmmss}";
                Console.WriteLine($"Running: add-migration {migrationName} ...");
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"ef migrations add {migrationName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Prevent child from infinitely looping
                processInfo.EnvironmentVariables["SKIP_ORCHESTRATOR"] = "1";

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        string err = await process.StandardError.ReadToEndAsync();
                        string outStr = await process.StandardOutput.ReadToEndAsync();
                        throw new Exception($"EF Core Migration Failed:\nOutput:\n{outStr}\nError:\n{err}");
                    }
                }

                // Analyze Migration Script
                Console.WriteLine("Scanning for generated Migration file...");
                var migrationFiles = Directory.GetFiles("Migrations", $"*{migrationName}.cs");
                if (migrationFiles.Length == 0)
                {
                    throw new Exception("Migration file was not generated successfully.");
                }

                string migrationCode = File.ReadAllText(migrationFiles[0]);
                
                Console.WriteLine("Passing Migration Script to Role C (Gemini) for safety analysis...");
                var migrationAgent = new MigrationAgent(geminiApiKey);
                string analysisResult = await migrationAgent.AnalyzeMigrationSafetyAsync(migrationCode);
                
                Console.WriteLine($"[Role C Verdict]: {analysisResult}");

                if (analysisResult.Trim().StartsWith("SAFE"))
                {
                    Console.WriteLine("Applying Database Update safely...");
                    var updateProcessInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "ef database update",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var updateProcess = Process.Start(updateProcessInfo);
                    updateProcess?.WaitForExit();
                    Console.WriteLine("Database Update Completed Successfully!");
                }
                else
                {
                    // Throw to ReporterAgent
                    throw new Exception($"Migration Agent blocked the update due to safety concerns:\n{analysisResult}");
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("=== [System Error Handler] Exception Caught ===");
                Console.WriteLine($"[Technical Error] {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("=== [Reporter Agent] Generating Human-Friendly Feedback ===");
                
                var reporterAgent = new SystemReporterAgent(geminiApiKey);
                var friendlyResponse = await reporterAgent.ReportErrorFriendlyAsync(ex.Message);
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Agent Message]:\n{friendlyResponse}");
                Console.ResetColor();
            }
        }
    }
}
