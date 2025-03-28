using Oracle.ManagedDataAccess.Client;

namespace DbListener.Services
{
    class OracleQueryTracker
    {
        private readonly string _connectionString;
        private readonly HashSet<string> _uniqueQueries;
        private readonly string _logFilePath;
        private Timer _timer;
        private DateTime _lastCheckTime;
        private Timer _stopTimer;

        public OracleQueryTracker(string connectionString, string logFilePath, int intervalMilliseconds, int durationMinutes)
        {
            // Установить переменную окружения TNS_ADMIN
            Environment.SetEnvironmentVariable("TNS_ADMIN", @"C:\oracle\odac64\network\admin");

            _connectionString = connectionString;
            _logFilePath = logFilePath;
            _uniqueQueries = new HashSet<string>();
            _lastCheckTime = DateTime.UtcNow;

            // Загрузить уже существующие уникальные запросы из файла, если он существует
            if (File.Exists(_logFilePath))
            {
                var existingQueries = File.ReadAllLines(_logFilePath);
                foreach (var query in existingQueries)
                {
                    _uniqueQueries.Add(query);
                }
            }

            // Настроить таймер для периодического отслеживания запросов
            _timer = new Timer(TrackQueries, null, 0, intervalMilliseconds);

            // Настроить таймер для автоматической остановки через заданное количество минут
            _stopTimer = new Timer(StopTracking, null, durationMinutes * 60 * 1000, Timeout.Infinite);

            Console.WriteLine($"Отслеживание запущено и автоматически остановится через {durationMinutes} минут(ы)");
        }

        private async void TrackQueries(object state)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine("Подключение к базе данных установлено.");

                    string query = @"
                        SELECT sql_text
                        FROM v$sql
                        WHERE parsing_schema_name NOT IN ('SYS', 'SYSTEM')
                        AND sql_text NOT LIKE '%v$sql%'
                        AND first_load_time > :lastCheckTime";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(":lastCheckTime", OracleDbType.Date, _lastCheckTime, System.Data.ParameterDirection.Input);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string sqlText = reader.GetString(0);
                                if (_uniqueQueries.Add(sqlText))
                                {
                                    // Записать уникальный запрос в файл
                                    await File.AppendAllTextAsync(_logFilePath, sqlText + Environment.NewLine);
                                    Console.WriteLine("Новый уникальный запрос записан: " + sqlText);
                                }
                            }
                        }
                    }

                    // Обновить время последней проверки
                    _lastCheckTime = DateTime.UtcNow;
                }
            }
            catch (OracleException ex)
            {
                Console.WriteLine($"Ошибка подключения к базе данных: {ex.Message}");
            }
        }

        private void StopTracking(object state)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _stopTimer?.Dispose();
            Console.WriteLine("Отслеживание автоматически остановлено по истечении времени.");
        }
    }
}