

using SqlParser.Net.Ast.Expression;
using SqlParser.Net;

namespace DbListener.Services
{
    public static class SqlAnalizer
    {
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
