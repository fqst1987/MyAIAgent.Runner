using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ClosedXML.Excel;

namespace MyAIAgent.Runner.Helper
{
    public class ExcelHelper
    {
        public async Task<string> ReadDirectoryAsync(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The directory {directoryPath} was not found.");
            }

            var files = Directory.GetFiles(directoryPath, "*.xlsx");
            var allTables = new List<object>();

            foreach (var file in files)
            {
                // To avoid locking issues, skip temp/hidden excel files starting with ~$
                if (Path.GetFileName(file).StartsWith("~$")) continue;

                var tableObject = await ProcessSingleFileAsync(file);
                if (tableObject != null)
                {
                    allTables.Add(tableObject);
                }
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            };
            
            return JsonSerializer.Serialize(allTables, options);
        }

        private async Task<object?> ProcessSingleFileAsync(string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException($"The file {filepath} was not found.");
            }

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filepath);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("No worksheets found in the Excel file.");
                }

                // 1. A2: Table name
                string tableName = worksheet.Cell("A2").GetValue<string>()?.Trim() ?? string.Empty;

                // 2. G2:I... : Related tables data
                // G1:I1 are headers (Relative_Table, Foreign_Key, Relative_Type)
                var relatedTables = new List<RelatedTableInfo>();
                int relRow = 2;
                while (true)
                {
                    string relTable = worksheet.Cell(relRow, 7).GetString()?.Trim();
                    string fKey = worksheet.Cell(relRow, 8).GetString()?.Trim();
                    string relType = worksheet.Cell(relRow, 9).GetString()?.Trim();

                    if (string.IsNullOrWhiteSpace(relTable) && string.IsNullOrWhiteSpace(fKey) && string.IsNullOrWhiteSpace(relType))
                    {
                        break;
                    }

                    relatedTables.Add(new RelatedTableInfo
                    {
                        RelativeTable = relTable,
                        ForeignKey = fKey,
                        RelativeType = relType
                    });
                    relRow++;
                }

                // 3. Columns data starting from Row 5
                // A4:E4 are headers: Column, Type, Nullable, Comment, Options
                var columns = new List<ColumnInfo>();
                int row = 5;
                while (true)
                {
                    string colNameRaw = worksheet.Cell(row, 1).GetString()?.Trim() ?? string.Empty;
                    
                    if (string.IsNullOrWhiteSpace(colNameRaw))
                    {
                        // Stop if we hit an empty row
                        break;
                    }

                    bool isKey = colNameRaw.Contains("*");
                    string cleanName = isKey ? colNameRaw.Replace("*", "").Trim() : colNameRaw;

                    var colInfo = new ColumnInfo
                    {
                        Name = cleanName,
                        IsKey = isKey,
                        Type = worksheet.Cell(row, 2).GetString()?.Trim() ?? string.Empty,
                        Nullable = worksheet.Cell(row, 3).GetString()?.Trim() ?? string.Empty,
                        Comment = worksheet.Cell(row, 4).GetString()?.Trim() ?? string.Empty,
                        Options = worksheet.Cell(row, 5).GetString()?.Trim() ?? string.Empty
                    };

                    columns.Add(colInfo);
                    row++;
                }

                return new
                {
                    TableName = tableName,
                    RelatedTables = relatedTables,
                    Columns = columns
                };
            });
        }
    }

    public class RelatedTableInfo
    {
        public string RelativeTable { get; set; } = string.Empty;
        public string ForeignKey { get; set; } = string.Empty;
        public string RelativeType { get; set; } = string.Empty;
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsKey { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Nullable { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Options { get; set; } = string.Empty;
    }
}
