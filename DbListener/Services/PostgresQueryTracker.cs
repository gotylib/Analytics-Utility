using Npgsql;
using SqlParser.Net;
using System.Text.Json;
using DbListener.Dal;
using DbListener.Dal.Entityes;
using Microsoft.EntityFrameworkCore;

namespace DbListener.Services
{
    public class PostgresQueryTracker : IDisposable
    {
        private readonly string _connectionString;
        private List<string> _queries;
        private Timer _timer;
        private bool _disposed;
        private ConnectionDbContext _context;
        private Connection _connection;
        private Task _backgroundTask;
        private CancellationTokenSource _cts;

        public PostgresQueryTracker(Connection connection,
                                  DbType dbType,
                                  ConnectionDbContext context)
        {
            _connectionString = $"Host={connection.Url};Port={connection.Port};Database={connection.DbName};" +
                              $"Username={connection.Name};Password={connection.Password};";
            
            _connection = connection;
            _context = context;
            _cts = new CancellationTokenSource();
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

                command.CommandText = "SELECT query FROM pg_stat_statements";
                using var reader = await command.ExecuteReaderAsync();

                _queries = new List<string>();
                while (await reader.ReadAsync())
                {
                    _queries.Add(reader["query"].ToString());
                }

                _timer?.Dispose();
                
                // Запускаем фоновую задачу
                _backgroundTask = Task.Run(async () => 
                {
                    try
                    {
                        await Task.Delay(minutes * 60 * 1000, _cts.Token);
                        
                        if (isNoise)
                        {
                            await StopTrackingSqlNoise();
                        }
                        else
                        {
                            await StopTrackingAndAnalyze();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Задача была отменена - это нормально
                    }
                }, _cts.Token);

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
            command.CommandText = "SELECT query FROM pg_stat_statements";
            command.Connection = connection;
            
            using var reader = await command.ExecuteReaderAsync();
            var sqlNoise = new List<SqlNoise>();
            while (await reader.ReadAsync())
            {
                sqlNoise.Add(new SqlNoise()
                {
                    Query = reader["query"].ToString(),
                    Connection = _connection,
                });
            }
            _context.SqlNoises.AddRange(sqlNoise);
            _connection.SqlNoise = sqlNoise;
            _context.Connections.Update(_connection);
            await _context.SaveChangesAsync();
            await connection.CloseAsync();
        }

        private async Task StopTrackingAndAnalyze()
        {
            if (_disposed) return;

            // Собираем запросы из pg_stat_statements
            await using var pgConnection = new NpgsqlConnection(_connectionString);
            await pgConnection.OpenAsync();

            var collectedQueries = new List<string>();
            using (var command = new NpgsqlCommand("SELECT query FROM pg_stat_statements", pgConnection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    collectedQueries.Add(reader["query"].ToString());
                }
            }

            // Фильтруем запросы для анализа
            var queriesToAnalyze = collectedQueries
                .Except(_queries, StringComparer.OrdinalIgnoreCase)
                .Except(_connection.SqlNoise.Select(q => q.Query), StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            // Создаем новый отчет
            var report = new Report
            {
                DateOfLog = DateTime.Now,
                ConnectionId = _connection.Id,
                Connection = _connection
            };

            var reportItems = new List<ReportItem>();

            // Анализируем каждый запрос
            foreach (var sql in queriesToAnalyze)
            {
                try
                {
                    var parseResult = SqlQueryParser.ParseSql(sql);
                    var tablesAndColumns = new Dictionary<string, List<string>>();

                    // Обрабатываем таблицы и столбцы
                    foreach (var table in parseResult.Tables)
                    {
                        foreach (var column in parseResult.Columns)
                        {
                            if (!tablesAndColumns.ContainsKey(table))
                            {
                                tablesAndColumns[table] = new List<string>();
                            }
                            tablesAndColumns[table].Add(column);
                        }
                    }

                    // Формируем строку с таблицами и столбцами
                    var tablesAndColumnsStr = string.Join("; ", 
                        tablesAndColumns.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value.Distinct())}"));
                    
                    reportItems.Add(new ReportItem
                    {
                        Query = sql,
                        TablesAndColumns = tablesAndColumnsStr
                    });
                }
                catch (Exception ex)
                { 
                    Console.WriteLine($"Ошибка при анализе запроса: {ex.Message}\nПроблемный запрос: {sql}\n");
                }
            }

            // Сохраняем в базу данных
            await using (var dbContext = new ConnectionDbContext())
            {
                // Добавляем отчет
                await dbContext.Reports.AddAsync(report);
                await dbContext.SaveChangesAsync();

                // Добавляем элементы отчета
                foreach (var item in reportItems)
                {
                    await dbContext.AddAsync(item);
                }

                await dbContext.SaveChangesAsync();
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Отменяем фоновую задачу
            _cts?.Cancel();
            
            // Дожидаемся завершения фоновой задачи
            _backgroundTask?.GetAwaiter().GetResult();
            
            _cts?.Dispose();
            _timer?.Dispose();
            
            // Убираем подавление финализации, чтобы GC мог собрать объект
            // GC.SuppressFinalize(this); // Убрали эту строку
        }
    }
}