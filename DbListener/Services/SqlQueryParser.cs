using System.Text.RegularExpressions;

namespace DbListener.Services
{
    public static class SqlQueryParser
    {
        public class ParseResult
        {
            public List<string> Tables { get; set; } = new List<string>();
            public List<string> Columns { get; set; } = new List<string>();
            public string QueryType { get; set; }
        }

        public static ParseResult ParseSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return new ParseResult();

            var result = new ParseResult();
            var normalizedSql = NormalizeSql(sql);

            DetermineQueryType(normalizedSql, result);
            ExtractTables(normalizedSql, result);
            ExtractColumns(normalizedSql, result);

            // Удаляем дубликаты
            result.Tables.RemoveAll(string.IsNullOrWhiteSpace);
            result.Columns.RemoveAll(string.IsNullOrWhiteSpace);
            result.Tables = result.Tables.Distinct().ToList();
            result.Columns = result.Columns.Distinct().ToList();

            return result;
        }

        private static string NormalizeSql(string sql)
        {
            // Удаляем комментарии
            sql = Regex.Replace(sql, @"--.*?$", " ", RegexOptions.Multiline);
            sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);

            // Заменяем несколько пробелов на один
            sql = Regex.Replace(sql, @"\s+", " ");

            // Приводим к нижнему регистру для упрощения парсинга
            return sql.ToLower();
        }

        private static void DetermineQueryType(string sql, ParseResult result)
        {
            if (sql.StartsWith("select"))
                result.QueryType = "SELECT";
            else if (sql.StartsWith("insert"))
                result.QueryType = "INSERT";
            else if (sql.StartsWith("update"))
                result.QueryType = "UPDATE";
            else if (sql.StartsWith("delete"))
                result.QueryType = "DELETE";
            else
                result.QueryType = "UNKNOWN";
        }

        private static void ExtractTables(string sql, ParseResult result)
        {
            // Извлекаем таблицы из FROM, JOIN, UPDATE, INSERT
            var fromMatches = Regex.Matches(sql, @"(?:from|join|update|into)\s+([^\s,(]+)");
            foreach (Match match in fromMatches)
            {
                if (match.Groups.Count > 1)
                {
                    var table = match.Groups[1].Value;
                    table = RemoveAlias(table);
                    result.Tables.Add(table);
                }
            }

            // Для INSERT также проверяем таблицу после INSERT INTO
            if (sql.StartsWith("insert"))
            {
                var insertMatch = Regex.Match(sql, @"insert\s+into\s+([^\s(]+)");
                if (insertMatch.Success && insertMatch.Groups.Count > 1)
                {
                    var table = insertMatch.Groups[1].Value;
                    table = RemoveAlias(table);
                    if (!result.Tables.Contains(table))
                        result.Tables.Add(table);
                }
            }
        }

        private static void ExtractColumns(string sql, ParseResult result)
        {
            // 1. Извлекаем колонки в SELECT
            if (sql.StartsWith("select"))
            {
                var selectMatch = Regex.Match(sql, @"select\s+(.*?)\s+from");
                if (selectMatch.Success)
                {
                    var columnsPart = selectMatch.Groups[1].Value;
                    ExtractColumnsFromPart(columnsPart, result);
                }
            }

            // 2. Извлекаем колонки в WHERE, GROUP BY, HAVING, ORDER BY
            var whereClauses = Regex.Matches(sql, @"(?:where|group by|having|order by)\s+([^)]+)");
            foreach (Match match in whereClauses)
            {
                if (match.Groups.Count > 1)
                {
                    ExtractColumnsFromPart(match.Groups[1].Value, result);
                }
            }

            // 3. Извлекаем колонки в INSERT (значения)
            if (sql.StartsWith("insert"))
            {
                var valuesMatch = Regex.Match(sql, @"values\s*\(([^)]+)");
                if (valuesMatch.Success)
                {
                    // В INSERT колонки указываются перед VALUES
                    var columnsMatch = Regex.Match(sql, @"insert\s+into\s+[^\s(]+\s*\(([^)]+)");
                    if (columnsMatch.Success)
                    {
                        ExtractColumnsFromPart(columnsMatch.Groups[1].Value, result);
                    }
                }
            }

            // 4. Извлекаем колонки в UPDATE (SET часть)
            if (sql.StartsWith("update"))
            {
                var setMatch = Regex.Match(sql, @"set\s+([^where]+)");
                if (setMatch.Success)
                {
                    ExtractColumnsFromPart(setMatch.Groups[1].Value, result);
                }
            }
        }

        private static void ExtractColumnsFromPart(string sqlPart, ParseResult result)
        {
            // Ищем конструкции вида table.column или просто column
            var columnMatches = Regex.Matches(sqlPart, @"(?:[\w.]+\.)?([\w]+)(?:\s*=\s*(?:[\w.]+\.)?[\w]+)?");

            foreach (Match match in columnMatches)
            {
                if (match.Groups.Count > 1)
                {
                    var column = match.Groups[1].Value;

                    // Игнорируем ключевые слова и числа
                    if (!IsSqlKeyword(column) && !int.TryParse(column, out _))
                    {
                        result.Columns.Add(column);
                    }
                }
            }
        }

        private static string RemoveAlias(string tableName)
        {
            // Удаляем алиасы типа "table as t" или "table t"
            var aliasMatch = Regex.Match(tableName, @"^([^\s]+)(?:\s+(?:as\s+)?\w+)?$");
            return aliasMatch.Success ? aliasMatch.Groups[1].Value : tableName;
        }

        private static bool IsSqlKeyword(string word)
        {
            var keywords = new HashSet<string> {
                "select", "from", "where", "group", "by", "having", "order", "join",
                "inner", "left", "right", "outer", "cross", "on", "as", "and", "or",
                "not", "in", "is", "null", "like", "between", "exists", "any", "all",
                "distinct", "case", "when", "then", "else", "end", "limit", "offset",
                "union", "except", "intersect", "insert", "into", "values", "update",
                "set", "delete", "create", "alter", "drop", "table", "view", "index",
                "primary", "key", "foreign", "references", "check", "default", "constraint",
                "unique", "asc", "desc", "count", "sum", "avg", "min", "max", "coalesce",
                "cast", "extract", "date", "time", "timestamp", "interval", "true", "false"
            };

            return keywords.Contains(word);
        }
    }
}