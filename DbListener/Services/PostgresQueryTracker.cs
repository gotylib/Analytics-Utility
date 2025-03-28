using Npgsql;
using SqlParser.Net;
using SqlParser.Net.Ast.Expression;

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
        private readonly SqlParser.Net.DbType _dbType;

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

            _timer = new Timer(TrackQueries, null, 0, intervalMilliseconds);
            _stopTimer = new Timer(StopTrackingAndAnalyze, null, durationMinutes * 60 * 1000, Timeout.Infinite);

            Console.WriteLine($"Отслеживание PostgreSQL запущено на {durationMinutes} минут");
        }

        private async void TrackQueries(object state)
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

        private async void StopTrackingAndAnalyze(object state)
        {
            _timer?.DisposeAsync();
            _stopTimer?.DisposeAsync();


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
            foreach (var sql in queriesToAnalyze)
            {
                Console.WriteLine($"\nАнализ запроса: {sql}");
                await AnalyzeSql(sql, _dbType);
            }

            Console.WriteLine("Отслеживание PostgreSQL завершено и все запросы проанализированы.");
        }

        public static async Task AnalyzeSql(string sql, DbType dbType)
        {
            try
            {
                var ast = DbUtils.Parse(sql, dbType);
                var tables = new HashSet<string>();
                var columns = new HashSet<string>();

                ExtractTablesAndColumns(ast, tables, columns);

                var path = @"C:\Users\Admin\Desktop\a.txt";

                await File.AppendAllTextAsync(path, $"\n\n=== Анализ запроса ===\n{sql}\n\n");

                await File.AppendAllTextAsync(path, "Таблицы:\n");
                foreach (var table in tables)
                {
                    await File.AppendAllTextAsync(path, $"- {table}\n");
                }

                await File.AppendAllTextAsync(path, "\nКолонки:\n");
                foreach (var column in columns)
                {
                    await File.AppendAllTextAsync(path, $"- {column}\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при анализе SQL: {ex.Message}");
            }
        }

        private static void ExtractTablesAndColumns(SqlExpression node, HashSet<string> tables, HashSet<string> columns)
        {
            if (node is SqlTableExpression tableExpr)
            {
                // Добавляем имя таблицы
                var tableName = tableExpr.Name.Value;
                if (tableExpr.Schema != null)
                {
                    tableName = $"{tableExpr.Schema.Value}.{tableName}";
                }
                tables.Add(tableName);

                // Если есть алиас, тоже добавляем его
                if (tableExpr.Alias != null)
                {
                    tables.Add(tableExpr.Alias.Value);
                }
            }
            else if (node is SqlPropertyExpression propExpr)
            {
                // Добавляем имя столбца в формате table.column или просто column
                var columnName = propExpr.Name.Value;
                if (propExpr.Table != null)
                {
                    columnName = $"{propExpr.Table.Value}.{columnName}";
                }
                columns.Add(columnName);
            }
            else if (node is SqlIdentifierExpression identExpr &&
                    !(node.Parent is SqlTableExpression || node.Parent is SqlPropertyExpression))
            {
                // Простые идентификаторы (не часть таблицы или свойства)
                columns.Add(identExpr.Value);
            }

            // Рекурсивно обходим дочерние узлы
            if (node is SqlSelectExpression selectExpr)
            {
                ExtractTablesAndColumns(selectExpr.Query, tables, columns);
            }
            else if (node is SqlSelectQueryExpression selectQueryExpr)
            {
                if (selectQueryExpr.From != null)
                {
                    ExtractTablesAndColumns(selectQueryExpr.From, tables, columns);
                }

                if (selectQueryExpr.Where != null)
                {
                    ExtractTablesAndColumns(selectQueryExpr.Where, tables, columns);
                }

                if (selectQueryExpr.Columns != null)
                {
                    foreach (var column in selectQueryExpr.Columns)
                    {
                        ExtractTablesAndColumns(column, tables, columns);
                    }
                }
            }
            else if (node is SqlSelectItemExpression selectItemExpr)
            {
                ExtractTablesAndColumns(selectItemExpr.Body, tables, columns);
            }
            else if (node is SqlJoinTableExpression joinExpr)
            {
                ExtractTablesAndColumns(joinExpr.Left, tables, columns);
                ExtractTablesAndColumns(joinExpr.Right, tables, columns);
                if (joinExpr.Conditions != null)
                {
                    ExtractTablesAndColumns(joinExpr.Conditions, tables, columns);
                }
            }
            else if (node is SqlBinaryExpression binaryExpr)
            {
                ExtractTablesAndColumns(binaryExpr.Left, tables, columns);
                ExtractTablesAndColumns(binaryExpr.Right, tables, columns);
            }
            // Добавьте обработку других типов выражений по необходимости
        }
    }
}