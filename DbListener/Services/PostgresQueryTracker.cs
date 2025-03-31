using Npgsql;
using SqlParser.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DbListener.Services
{
    public class PostgresQueryTracker : IDisposable
    {
        private readonly string _connectionString;
        private readonly List<string> _capturedQueries;
        private readonly string _logFilePath;
        private readonly object _collectionLock = new object();
        private Timer _timer;
        private DateTime _lastCheckTime;
        private Timer _stopTimer;
        private readonly DbType _dbType;
        private bool _disposed;

        public PostgresQueryTracker(string host, int port, string database,
                                  string username, string password,
                                  string logFilePath, int intervalMilliseconds,
                                  int durationMinutes, DbType dbType)
        {
            _connectionString = $"Host={host};Port={port};Database={database};" +
                              $"Username={username};Password={password};";

            _logFilePath = logFilePath;
            _capturedQueries = new List<string>();
            _lastCheckTime = DateTime.UtcNow;
            _dbType = dbType;

            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }

            _timer = new Timer(async _ => await TrackQueries(), null, 0, intervalMilliseconds);
            _stopTimer = new Timer(_ => StopTrackingAndAnalyze().Wait(),
                                 null,
                                 durationMinutes * 60 * 1000,
                                 Timeout.Infinite);

            Console.WriteLine($"Отслеживание PostgreSQL запущено на {durationMinutes} минут");
        }

        private async Task<string> ReplaceParametersWithRealValues(int pid, string sql)
        {
            try
            {
                // Пробуем получить реальные параметры из pg_stat_activity
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var paramQuery = @"
                    SELECT param_values
                    FROM pg_stat_activity
                    WHERE pid = @pid";

                await using var paramCommand = new NpgsqlCommand(paramQuery, connection);
                paramCommand.Parameters.AddWithValue("@pid", pid);

                var paramValues = await paramCommand.ExecuteScalarAsync() as string;

                if (string.IsNullOrEmpty(paramValues))
                {
                    return sql; // Не смогли получить параметры - возвращаем оригинальный запрос
                }

                // Парсим параметры (пример формата: {$1=123, $2='text'})
                var paramDict = new Dictionary<int, string>();
                var matches = Regex.Matches(paramValues, @"\$(\d+)=([^,]+)");

                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Groups[1].Value, out var paramNum))
                    {
                        paramDict[paramNum] = match.Groups[2].Value;
                    }
                }

                if (paramDict.Count == 0)
                {
                    return sql;
                }

                // Заменяем параметры в SQL
                var processedSql = new StringBuilder(sql);
                var paramMatches = Regex.Matches(sql, @"\$\d+");

                foreach (Match match in paramMatches)
                {
                    string param = match.Value;
                    if (int.TryParse(param.Substring(1), out var paramNum) && paramDict.TryGetValue(paramNum, out var value))
                    {
                        processedSql.Replace(param, value);
                    }
                }

                return processedSql.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при замене параметров: {ex.Message}");
                return sql;
            }
        }

        private async Task TrackQueries()
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT pid, query, query_start
                    FROM pg_stat_activity
                    WHERE pid <> pg_backend_pid()
                    AND query_start > @lastCheckTime
                    AND state = 'active'
                    ORDER BY query_start DESC
                    LIMIT 100";

                await using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@lastCheckTime", _lastCheckTime);

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int pid = reader.GetInt32(0);
                    string originalSql = reader.GetString(1);
                    string processedSql = originalSql;

                    if (originalSql.Contains("$"))
                    {
                        processedSql = await ReplaceParametersWithRealValues(pid, originalSql);
                        Console.WriteLine($"Обработан запрос с параметрами:\n{originalSql}\n=>\n{processedSql}");
                    }

                    lock (_collectionLock)
                    {
                        if (!_capturedQueries.Contains(processedSql))
                        {
                            _capturedQueries.Add(processedSql);
                            File.AppendAllText(_logFilePath, processedSql + Environment.NewLine);
                        }
                    }
                }

                _lastCheckTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отслеживании запросов: {ex.Message}");
            }
        }

        private async Task StopTrackingAndAnalyze()
        {
            if (_disposed) return;

            _timer?.Dispose();
            _stopTimer?.Dispose();

            Console.WriteLine("\nНачало анализа собранных SQL-запросов...");

            List<string> queriesToAnalyze;
            lock (_collectionLock)
            {
                queriesToAnalyze = new List<string>(_capturedQueries);
            }

            var uniqueQueries = queriesToAnalyze
                .Distinct()
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToList();

            Console.WriteLine($"Найдено {uniqueQueries.Count} уникальных запросов");

            // Словарь для хранения результатов: таблица -> список колонок
            var result = new Dictionary<string, HashSet<string>>();

            foreach (var sql in uniqueQueries)
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
            _timer?.Dispose();
            _stopTimer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}