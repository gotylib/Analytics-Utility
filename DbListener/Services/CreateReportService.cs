using SqlParser.Net;
using System.Text.Json;

namespace DbListener.Services
{
    public class CreateReportService
    {
        private readonly List<string> _capturedQueries = new List<string>();
        private readonly HashSet<string> _uniqueQueries = new HashSet<string>();
        private readonly string _internalQueryIdentifier;

        public CreateReportService(string internalQueryIdentifier)
        {
            _internalQueryIdentifier = internalQueryIdentifier;
        }

        // Метод для захвата запроса
        public void CaptureQuery(string query)
        {
            if (!IsInternalQuery(query))
            {
                _capturedQueries.Add(query);
            }
        }

        // Обработка захваченных запросов для выделения уникальных
        public void ProcessCapturedQueries()
        {
            _uniqueQueries.Clear();
            foreach (var query in _capturedQueries)
            {
                _uniqueQueries.Add(query);
            }
        }

        // Генерация отчетов
        //public async Task GenerateReportsAsync(string outputDirectory)
        //{
        //    await GenerateTextReportAsync(outputDirectory);
        //    await GenerateJsonReportAsync(outputDirectory);
        //}

        //// Генерация текстового отчета
        //private async Task GenerateTextReportAsync(string outputDirectory)
        //{
        //    var reportPath = Path.Combine(outputDirectory, "queries_report.txt");
        //    await File.WriteAllLinesAsync(reportPath, _uniqueQueries);
        //}

        // Генерация JSON отчета
        //private async Task GenerateJsonReportAsync(string outputDirectory)
        //{
        //    var tableColumnsUsage = ExtractTableColumnsUsage();
        //    var reportPath = Path.Combine(outputDirectory, "table_columns_report.json");
        //    var jsonContent = JsonSerializer.Serialize(tableColumnsUsage, new JsonSerializerOptions { WriteIndented = true });
        //    await File.WriteAllTextAsync(reportPath, jsonContent);
        //}

        // Извлечение информации о таблицах и столбцах
        //private Dictionary<string, HashSet<string>> ExtractTableColumnsUsage()
        //{
        //    var tableColumnsUsage = new Dictionary<string, HashSet<string>>();

        //    foreach (var query in _uniqueQueries)
        //    {
        //        var tablesAndColumns = ParseQuery(query);

        //        foreach (var table in tablesAndColumns.Keys)
        //        {
        //            if (!tableColumnsUsage.ContainsKey(table))
        //            {
        //                tableColumnsUsage[table] = new HashSet<string>();
        //            }

        //            foreach (var column in tablesAndColumns[table])
        //            {
        //                tableColumnsUsage[table].Add(column);
        //            }
        //        }
        //    }

        //    return tableColumnsUsage;
        //}

        // Парсинг запроса для извлечения таблиц и столбцов
        private void ParseQuery(string query)
        {
            try
            {

                var sql = "select id AS bid,t.NAME testName  from test t";
                var sqlAst = DbUtils.Parse(sql, DbType.Oracle);
            }
            catch (Exception ex)
            {

            }

        }

        // Проверка, является ли запрос внутренним
        private bool IsInternalQuery(string query)
        {
            return query.Contains(_internalQueryIdentifier, StringComparison.OrdinalIgnoreCase);
        }
    }
}
 