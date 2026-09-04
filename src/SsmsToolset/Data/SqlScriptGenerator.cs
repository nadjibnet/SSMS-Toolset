using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace SsmsToolset.Data
{
    /// <summary>
    /// Produces T-SQL for the Object Explorer-style actions: SELECT TOP N and
    /// "script as CREATE". Programmable objects (views, procedures, functions)
    /// are scripted with <c>OBJECT_DEFINITION</c>; tables get a basic CREATE
    /// TABLE built from the catalog views.
    /// </summary>
    public static class SqlScriptGenerator
    {
        public static bool SupportsSelectTop(DatabaseObject o)
            => o != null && (o.TypeCode == "U" || o.TypeCode == "V");

        public static string SelectTop(DatabaseObject o, int count)
            => $"SELECT    TOP ({count})\n        *\nFROM    {Quote(o.Schema)}.{Quote(o.Name)}\nWHERE    1 = 1\n-- AND    ";

        /// <summary>
        /// A SELECT TOP N that lists every column explicitly (round-trips to the DB
        /// for the column names). Falls back to <c>*</c> if none are found.
        /// </summary>
        public static string SelectTopAllColumns(string connectionString, DatabaseObject o, int count)
        {
            var columns = GetColumnNames(connectionString, o);
            if (columns.Count == 0)
            {
                return SelectTop(o, count);
            }

            var sb = new StringBuilder();
            sb.Append($"SELECT    TOP ({count})\n");
            for (int i = 0; i < columns.Count; i++)
            {
                sb.Append("        ").Append(Quote(columns[i]));
                if (i < columns.Count - 1) { sb.Append(','); }
                sb.Append('\n');
            }
            sb.Append($"FROM    {Quote(o.Schema)}.{Quote(o.Name)}\nWHERE    1 = 1\n-- AND    ");
            return sb.ToString();
        }

        /// <summary>
        /// The SSMS <c>Alt+F1</c> equivalent: <c>EXEC sp_help</c> on the object,
        /// which returns its full definition (columns, types, indexes, constraints,
        /// foreign keys, ...) as a set of result grids. Works for any object type.
        /// </summary>
        public static string ObjectInfo(DatabaseObject o)
        {
            string qualified = $"{Quote(o.Schema)}.{Quote(o.Name)}".Replace("'", "''");
            return $"EXEC sp_help N'{qualified}'";
        }

        /// <summary>
        /// Builds a single script with a <c>SELECT TOP 10</c> for every user table
        /// whose name contains "Migration". Each is ordered by <c>MigrationId DESC</c>
        /// when that column exists (skipped otherwise so the query still runs).
        /// </summary>
        public static string BuildMigrationSamples(string connectionString)
        {
            const string query = @"
SELECT s.name AS SchemaName, o.name AS TableName,
       CASE WHEN EXISTS (
           SELECT 1 FROM sys.columns c
           WHERE c.object_id = o.object_id AND c.name = 'MigrationId'
       ) THEN 1 ELSE 0 END AS HasMigrationId
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.type = 'U' AND o.is_ms_shipped = 0
  AND o.name LIKE '%Migration%'
ORDER BY s.name, o.name;";

            var blocks = new List<string>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string obj = $"{Quote(reader.GetString(0))}.{Quote(reader.GetString(1))}";
                        bool hasMigrationId = reader.GetInt32(2) == 1;
                        string block = $"-- {obj}\nSELECT TOP 10 *\nFROM {obj}";
                        if (hasMigrationId)
                        {
                            block += "\nORDER BY [MigrationId] DESC";
                        }
                        blocks.Add(block);
                    }
                }
            }

            return blocks.Count == 0
                ? "-- No tables containing 'Migration' were found."
                : "-- Migration tables (TOP 10, newest first)\n\n" + string.Join("\n\n", blocks);
        }

        /// <summary>
        /// An UPDATE that lists every column (round-trips to the DB). The UPDATE and
        /// WHERE lines are live, but every column assignment is commented out — the
        /// user uncomments only the columns they actually want to set. The WHERE
        /// clause is keyed on the primary key (or the first column if there is none).
        /// </summary>
        public static string UpdateStatement(string connectionString, DatabaseObject o)
        {
            GetColumnsAndKeys(connectionString, o, out var columns, out var keys);

            var sb = new StringBuilder();
            sb.Append($"UPDATE    {Quote(o.Schema)}.{Quote(o.Name)} \nSET\n");
            if (columns.Count == 0)
            {
                sb.Append("--        [Column] = <value>\n");
            }
            else
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    sb.Append("--        ").Append(Quote(columns[i])).Append(" = <value>");
                    if (i < columns.Count - 1) { sb.Append(','); }
                    sb.Append('\n');
                }
            }
            sb.Append("WHERE    ").Append(BuildWhere(keys, columns));
            return sb.ToString();
        }

        /// <summary>
        /// A DELETE with a WHERE clause keyed on the primary key (or the first
        /// column if there is none) so it never deletes the whole table blindly.
        /// </summary>
        public static string DeleteStatement(string connectionString, DatabaseObject o)
        {
            GetColumnsAndKeys(connectionString, o, out var columns, out var keys);
            return $"DELETE \nFROM    {Quote(o.Schema)}.{Quote(o.Name)}\nWHERE    {BuildWhere(keys, columns)}";
        }

        /// <summary>
        /// A ready-to-fill invocation template (round-trips to the DB for the
        /// parameter list): <c>EXEC</c> for procedures, <c>SELECT ... FROM fn(...)</c>
        /// for table-valued functions, and <c>SELECT fn(...)</c> for scalar ones.
        /// Placeholders show the parameter's data type; the template is not executed.
        /// </summary>
        public static string ExecTemplate(string connectionString, DatabaseObject o)
        {
            var parameters = GetParameters(connectionString, o);
            string obj = $"{Quote(o.Schema)}.{Quote(o.Name)}";

            if (o.TypeCode == "P")
            {
                if (parameters.Count == 0)
                {
                    return $"EXEC {obj}";
                }

                var sb = new StringBuilder();
                sb.Append($"EXEC {obj}\n");
                for (int i = 0; i < parameters.Count; i++)
                {
                    var p = parameters[i];
                    sb.Append("    ").Append(p.Name).Append(" = <").Append(p.Type).Append('>');
                    if (p.IsOutput) { sb.Append(" OUTPUT"); }
                    if (i < parameters.Count - 1) { sb.Append(','); }
                    sb.Append('\n');
                }
                return sb.ToString().TrimEnd('\n');
            }

            // Functions take positional arguments.
            string args = string.Join(", ", parameters.Select(p => $"<{p.Name.TrimStart('@')}>"));
            bool tableValued = o.TypeCode == "IF" || o.TypeCode == "TF" || o.TypeCode == "FT";
            return tableValued
                ? $"SELECT *\nFROM {obj}({args})"
                : $"SELECT {obj}({args}) AS Result";
        }

        private sealed class ParamInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool IsOutput { get; set; }
        }

        private static List<ParamInfo> GetParameters(string connectionString, DatabaseObject o)
        {
            var parameters = new List<ParamInfo>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                const string query = @"
SELECT p.name, TYPE_NAME(p.user_type_id) AS type_name, p.is_output
FROM sys.parameters p
WHERE p.object_id = OBJECT_ID(@fullName) AND p.parameter_id > 0 AND p.name <> ''
ORDER BY p.parameter_id;";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@fullName", o.FullName);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            parameters.Add(new ParamInfo
                            {
                                Name = reader.GetString(0),
                                Type = reader.IsDBNull(1) ? "sql_variant" : reader.GetString(1),
                                IsOutput = !reader.IsDBNull(2) && reader.GetBoolean(2)
                            });
                        }
                    }
                }
            }
            return parameters;
        }

        /// <summary>Builds a "[key] = &lt;value&gt; AND ..." predicate for UPDATE/DELETE.</summary>
        private static string BuildWhere(List<string> keys, List<string> columns)
        {
            var cols = keys.Count > 0
                ? keys
                : (columns.Count > 0 ? new List<string> { columns[0] } : null);

            if (cols == null)
            {
                return "[id] = <value>";
            }

            var parts = new List<string>();
            foreach (var c in cols)
            {
                parts.Add($"{Quote(c)} = <value>");
            }
            return string.Join(" AND ", parts);
        }

        private static List<string> GetColumnNames(string connectionString, DatabaseObject o)
        {
            var names = new List<string>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                ReadColumnNames(connection, o, names);
            }
            return names;
        }

        /// <summary>Reads column names and primary-key columns in one connection.</summary>
        private static void GetColumnsAndKeys(
            string connectionString, DatabaseObject o, out List<string> columns, out List<string> keys)
        {
            columns = new List<string>();
            keys = new List<string>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                ReadColumnNames(connection, o, columns);

                const string pkQuery = @"
SELECT c.name
FROM sys.indexes i
INNER JOIN sys.index_columns ic
       ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns c
       ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.is_primary_key = 1 AND i.object_id = OBJECT_ID(@fullName)
ORDER BY ic.key_ordinal;";
                using (var command = new SqlCommand(pkQuery, connection))
                {
                    command.Parameters.AddWithValue("@fullName", o.FullName);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            keys.Add(reader.GetString(0));
                        }
                    }
                }
            }
        }

        private static void ReadColumnNames(SqlConnection connection, DatabaseObject o, List<string> into)
        {
            using (var command = new SqlCommand(
                "SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@fullName) ORDER BY c.column_id;",
                connection))
            {
                command.Parameters.AddWithValue("@fullName", o.FullName);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        into.Add(reader.GetString(0));
                    }
                }
            }
        }

        /// <summary>Returns a CREATE script for the object (round-trips to the DB).</summary>
        public static string BuildCreateScript(string connectionString, DatabaseObject o)
        {
            if (o.TypeCode == "U")
            {
                return BuildCreateTable(connectionString, o);
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    "SELECT OBJECT_DEFINITION(OBJECT_ID(@fullName));", connection))
                {
                    command.Parameters.AddWithValue("@fullName", o.FullName);
                    object result = command.ExecuteScalar();
                    string definition = result as string;
                    return string.IsNullOrEmpty(definition)
                        ? $"-- No definition available for {o.FullName}."
                        : definition;
                }
            }
        }

        private static string BuildCreateTable(string connectionString, DatabaseObject o)
        {
            var columns = new StringBuilder();
            string primaryKey = null;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                const string columnsQuery = @"
SELECT c.name, t.name AS type_name, c.max_length, c.precision, c.scale,
       c.is_nullable, c.is_identity,
       ic.seed_value, ic.increment_value
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.identity_columns ic
       ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE c.object_id = OBJECT_ID(@fullName)
ORDER BY c.column_id;";

                using (var command = new SqlCommand(columnsQuery, connection))
                {
                    command.Parameters.AddWithValue("@fullName", o.FullName);
                    using (var reader = command.ExecuteReader())
                    {
                        bool first = true;
                        while (reader.Read())
                        {
                            if (!first) { columns.Append(",\n"); }
                            first = false;

                            string name = reader.GetString(0);
                            string type = FormatType(
                                reader.GetString(1),
                                reader.GetInt16(2),
                                reader.GetByte(3),
                                reader.GetByte(4));
                            bool nullable = reader.GetBoolean(5);
                            bool identity = reader.GetBoolean(6);

                            columns.Append("    ").Append(Quote(name)).Append(' ').Append(type);
                            if (identity)
                            {
                                long seed = reader.IsDBNull(7) ? 1 : System.Convert.ToInt64(reader.GetValue(7));
                                long incr = reader.IsDBNull(8) ? 1 : System.Convert.ToInt64(reader.GetValue(8));
                                columns.Append($" IDENTITY({seed},{incr})");
                            }
                            columns.Append(nullable ? " NULL" : " NOT NULL");
                        }
                    }
                }

                const string pkQuery = @"
SELECT c.name
FROM sys.indexes i
INNER JOIN sys.index_columns ic
       ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns c
       ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.is_primary_key = 1 AND i.object_id = OBJECT_ID(@fullName)
ORDER BY ic.key_ordinal;";

                using (var command = new SqlCommand(pkQuery, connection))
                {
                    command.Parameters.AddWithValue("@fullName", o.FullName);
                    var pkColumns = new StringBuilder();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (pkColumns.Length > 0) { pkColumns.Append(", "); }
                            pkColumns.Append(Quote(reader.GetString(0)));
                        }
                    }
                    if (pkColumns.Length > 0)
                    {
                        primaryKey = pkColumns.ToString();
                    }
                }
            }

            var script = new StringBuilder();
            script.Append($"CREATE TABLE {Quote(o.Schema)}.{Quote(o.Name)} (\n");
            script.Append(columns);
            if (primaryKey != null)
            {
                script.Append($",\n    PRIMARY KEY ({primaryKey})");
            }
            script.Append("\n)");
            return script.ToString();
        }

        private static string FormatType(string typeName, int maxLength, int precision, int scale)
        {
            switch (typeName)
            {
                case "varchar":
                case "char":
                case "varbinary":
                case "binary":
                    return maxLength == -1 ? $"{typeName}(max)" : $"{typeName}({maxLength})";
                case "nvarchar":
                case "nchar":
                    return maxLength == -1 ? $"{typeName}(max)" : $"{typeName}({maxLength / 2})";
                case "decimal":
                case "numeric":
                    return $"{typeName}({precision},{scale})";
                case "datetime2":
                case "time":
                case "datetimeoffset":
                    return $"{typeName}({scale})";
                default:
                    return typeName;
            }
        }

        private static string Quote(string identifier)
            => "[" + identifier.Replace("]", "]]") + "]";
    }
}
