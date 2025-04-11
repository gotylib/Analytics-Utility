using Npgsql;
using SqlParser.Net;
using System.Text.Json;

namespace DbListener.Services
{
    public class PostgresQueryTracker : IDisposable
    {
        private readonly string _connectionString;
        private readonly string _logFilePath;
        private List<string> _queryes;
        private List<string> _sqlNoise = new List<string>();
        private Timer _timer;
        private bool _disposed;

        public PostgresQueryTracker(string host, int port, string database,
                                  string username, string password,
                                  string logFilePath, int intervalMilliseconds,
                                  int durationMinutes, DbType dbType)
        {
            _connectionString = $"Host={host};Port={port};Database={database};" +
                              $"Username={username};Password={password};";

            _logFilePath = logFilePath;

            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
        }

        public async Task<ResultModel<object, Exception>> StartTracking(int minutes, bool isNoise)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new NpgsqlCommand();

                command.CommandText = "SELECT pg_stat_statements_reset()";
                command.Connection = connection;
                await command.ExecuteNonQueryAsync();

                command.CommandText = @"
                SELECT
                    query
                FROM pg_stat_statements";

                using var reader = await command.ExecuteReaderAsync();

                _queryes = new List<string>();

                while (await reader.ReadAsync())
                {
                    _queryes.Add(reader["query"].ToString());
                }

                _timer?.Dispose();
                if (isNoise)
                {
                    _timer = new Timer(async _ =>
                    {
                        await StopTrackingSqlNoise();
                        _timer?.Dispose();
                    }, null, minutes * 60 * 1000, Timeout.Infinite);

                    return ResultModel<object, Exception>.CreateSuccessfulResult();
                }
                _timer = new Timer(async _ =>
                {
                    await StopTrackingAndAnalyze();
                    _timer?.Dispose();
                }, null, minutes * 60 * 1000, Timeout.Infinite);

                return ResultModel<object, Exception>.CreateSuccessfulResult();
            }
            catch (Exception ex)
            {
                return ResultModel<object, Exception>.CreateFailedResult(ex);
            }


        }

        public async Task StopTrackingSqlNoise()
        {
            if (_disposed) return;


            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand();

            command.CommandText = @"
                SELECT
                    query
                FROM pg_stat_statements";

            command.Connection = connection;
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _sqlNoise.Add(reader["query"].ToString());
            }
        }

        private async Task StopTrackingAndAnalyze()
        {
            if (_disposed) return;


            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand();

            command.CommandText = @"
                SELECT
                    query
                FROM pg_stat_statements";

            command.Connection = connection;
            using var reader = await command.ExecuteReaderAsync();

            var collectedQueryes = new List<string>();

            while (await reader.ReadAsync())
            {
                collectedQueryes.Add(reader["query"].ToString());
            }

            var queriesToAnalyze = collectedQueryes
                .Except(_queryes, StringComparer.OrdinalIgnoreCase)
                .Except(_sqlNoise, StringComparer.OrdinalIgnoreCase)
                .ToList();


            // Словарь для хранения результатов: таблица -> список колонок
            var result = new Dictionary<string, HashSet<string>>();

            foreach (var sql in queriesToAnalyze)
            {
                try
                {
                    var parseResult = SqlQueryParser.ParseSql(sql);

                    // Добавляем найденные таблицы и колонки в результат
                    foreach (var table in parseResult.Tables)
                    {
                        if (!result.ContainsKey(table))
                        {
                            result[table] = new HashSet<string>();
                        }

                        foreach (var column in parseResult.Columns)
                        {
                            result[table].Add(column);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при анализе запроса: {ex.Message}");
                    Console.WriteLine($"Проблемный запрос: {sql}");
                }
            }

            // Сохраняем результат в JSON
            var outputPath = @"C:\Users\Admin\Desktop\a.json";
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Преобразуем HashSet в List для сериализации
            var serializableResult = result.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(serializableResult, options));
            Console.WriteLine($"Результаты анализа сохранены в {outputPath}");
        }


        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}