using Npgsql;
using SqlParser.Net;

namespace DbListener.Services
{
    class PostgresQueryTracker
    {
        private readonly string _connectionString;
        private readonly List<string> _capturedQueries;
        private readonly string _logFilePath;
        private readonly Lock _collectionLock = new Lock();
        private Timer _timer;
        private DateTime _lastCheckTime;
        private Timer _stopTimer;
        private readonly DbType _dbType;

        public PostgresQueryTracker(string host, int port, string database,
                                  string username, string password,
                                  string logFilePath, int intervalMilliseconds,
                                  int durationMinutes, DbType dbType)
        {
            _connectionString = $"Host={host};Port={port};Database={database};" +
                              $"Username={username};Password={password}";

            _logFilePath = logFilePath;
            _capturedQueries = new List<string>();
            _lastCheckTime = DateTime.UtcNow;
            _dbType = dbType;

            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath); // Очищаем предыдущий лог
            }

            _timer = new Timer(async _ => await TrackQueries(), null, 0, intervalMilliseconds);
            _stopTimer = new Timer(async _ => await StopTrackingAndAnalyze(), null, durationMinutes * 60 * 1000, Timeout.Infinite);

            Console.WriteLine($"Отслеживание PostgreSQL запущено на {durationMinutes} минут");
        }

        private async Task TrackQueries()
        {
            try
            {
                await using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT query
                        FROM pg_stat_activity
                        WHERE pid <> pg_backend_pid()
                        AND query_start > @lastCheckTime
                        AND state = 'active'";

                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@lastCheckTime", _lastCheckTime);

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string sqlText = reader.GetString(0);

                                lock (_collectionLock)
                                {
                                    _capturedQueries.Add(sqlText);
                                }

                                await File.AppendAllTextAsync(_logFilePath, sqlText + Environment.NewLine);
                                Console.WriteLine($"Запрос записан: {sqlText}");
                            }
                        }
                    }

                    _lastCheckTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private async Task StopTrackingAndAnalyze()
        {
            _timer?.Dispose();
            _stopTimer?.Dispose();

            Console.WriteLine("\nАнализ собранных SQL-запросов:");

            // 1. Сначала получаем безопасную копию коллекции
            List<string> queriesToAnalyze;
            lock (_collectionLock)
            {
                queriesToAnalyze = new List<string>(_capturedQueries);
            }

            // 2. Фильтруем только уникальные запросы (если нужно)
            var uniqueQueries = queriesToAnalyze
                .Distinct()
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToList();

            // Анализируем все собранные запросы
            var analyzeTasks = uniqueQueries.Select(sql => SqlAnalizer.AnalyzeSql(sql, _dbType));
            await Task.WhenAll(analyzeTasks);

            Console.WriteLine("Отслеживание PostgreSQL завершено и все запросы проанализированы.");
        }

        
    }
}