using System.Data.SqlClient;
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
            => $"SELECT TOP ({count}) *\nFROM {Quote(o.Schema)}.{Quote(o.Name)};";

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
            script.Append("\n);");
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
