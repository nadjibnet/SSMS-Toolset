using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace SsmsToolset.Data
{
    /// <summary>
    /// Loads the object inventory (tables, views, procedures, functions) of a
    /// database from its catalog views, using the caller's connection string.
    /// Only the requested object categories are queried.
    /// </summary>
    public static class DatabaseObjectService
    {
        public static List<DatabaseObject> Load(
            string connectionString,
            bool tables,
            bool views,
            bool procedures,
            bool functions,
            bool includeColumnsParams = false)
        {
            var typeCodes = new List<string>();
            if (tables) typeCodes.Add("'U'");
            if (views) typeCodes.Add("'V'");
            if (procedures) typeCodes.Add("'P'");
            if (functions) typeCodes.AddRange(new[] { "'FN'", "'IF'", "'TF'", "'FS'", "'FT'", "'AF'" });

            var objects = new List<DatabaseObject>();
            if (typeCodes.Count == 0)
            {
                return objects;
            }

            // The type codes are a fixed, trusted set — safe to inline.
            string query = $@"
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type AS TypeCode, o.object_id AS ObjectId
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON o.schema_id = s.schema_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ({string.Join(",", typeCodes)})
ORDER BY s.name, o.name;";

            var byId = new Dictionary<int, DatabaseObject>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        string typeCode = reader.GetString(2).Trim();
                        int objectId = reader.GetInt32(3);
                        var obj = new DatabaseObject
                        {
                            Schema = schema,
                            Name = name,
                            ObjectId = objectId,
                            TypeCode = typeCode,
                            TypeLabel = LabelFor(typeCode),
                            SearchKey = TextNormalizer.Normalize($"{schema}.{name}")
                        };
                        objects.Add(obj);
                        byId[objectId] = obj;
                    }
                }

                if (includeColumnsParams)
                {
                    PopulateColumnsAndParams(connection, byId, tables || views, procedures || functions);
                }
            }

            return objects;
        }

        /// <summary>
        /// Fills each object's <see cref="DatabaseObject.ColumnsOrParams"/> using at
        /// most two set-based queries (columns for tables/views, parameters for
        /// procedures/functions) — never one round-trip per object.
        /// </summary>
        private static void PopulateColumnsAndParams(
            SqlConnection connection,
            Dictionary<int, DatabaseObject> byId,
            bool wantColumns,
            bool wantParams)
        {
            var tokens = new Dictionary<int, List<ColumnToken>>();

            void Add(int objectId, ColumnToken token)
            {
                if (!byId.ContainsKey(objectId))
                {
                    return;
                }
                if (!tokens.TryGetValue(objectId, out var list))
                {
                    list = new List<ColumnToken>();
                    tokens[objectId] = list;
                }
                list.Add(token);
            }

            if (wantColumns)
            {
                // Columns of tables/views, flagged as primary key (any PK index
                // column) and/or foreign key (any FK parent column).
                const string columnsQuery = @"
SELECT c.object_id, c.name,
       CASE WHEN EXISTS (
           SELECT 1 FROM sys.indexes i
           INNER JOIN sys.index_columns ic
               ON ic.object_id = i.object_id AND ic.index_id = i.index_id
           WHERE i.is_primary_key = 1
             AND i.object_id = c.object_id AND ic.column_id = c.column_id
       ) THEN 1 ELSE 0 END AS IsPrimaryKey,
       CASE WHEN EXISTS (
           SELECT 1 FROM sys.foreign_key_columns fkc
           WHERE fkc.parent_object_id = c.object_id
             AND fkc.parent_column_id = c.column_id
       ) THEN 1 ELSE 0 END AS IsForeignKey
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('U','V')
ORDER BY c.object_id, c.column_id;";
                using (var command = new SqlCommand(columnsQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Add(reader.GetInt32(0), new ColumnToken
                        {
                            Name = reader.GetString(1),
                            IsPrimaryKey = reader.GetInt32(2) == 1,
                            IsForeignKey = reader.GetInt32(3) == 1
                        });
                    }
                }
            }

            if (wantParams)
            {
                const string paramsQuery = @"
SELECT p.object_id, p.name
FROM sys.parameters p
INNER JOIN sys.objects o ON o.object_id = p.object_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('P','FN','IF','TF','FS','FT','AF')
  AND p.name <> ''
ORDER BY p.object_id, p.parameter_id;";
                using (var command = new SqlCommand(paramsQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Add(reader.GetInt32(0), new ColumnToken { Name = reader.GetString(1) });
                    }
                }
            }

            foreach (var pair in tokens)
            {
                var obj = byId[pair.Key];
                obj.ColumnTokens = pair.Value;
                obj.ColumnsOrParams = BuildDisplayList(pair.Value);
            }
        }

        /// <summary>
        /// Plain-text form of a token list (for the cell tooltip), prefixing keys
        /// with <c>[pk]</c> / <c>[fk]</c> to match the grid's rendering.
        /// </summary>
        private static string BuildDisplayList(List<ColumnToken> tokens)
        {
            var sb = new StringBuilder();
            foreach (var token in tokens)
            {
                if (sb.Length > 0) { sb.Append(", "); }
                if (token.IsPrimaryKey) { sb.Append("[pk]"); }
                else if (token.IsForeignKey) { sb.Append("[fk]"); }
                sb.Append(token.Name);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Finds programmable objects (views, procedures, functions) whose T-SQL
        /// body contains <paramref name="term"/>, via <c>sys.sql_modules</c>. Only
        /// the enabled programmable types are searched; tables have no body.
        /// </summary>
        public static List<DatabaseObject> SearchDefinitions(
            string connectionString,
            string term,
            bool views,
            bool procedures,
            bool functions)
        {
            var typeCodes = new List<string>();
            if (views) typeCodes.Add("'V'");
            if (procedures) typeCodes.Add("'P'");
            if (functions) typeCodes.AddRange(new[] { "'FN'", "'IF'", "'TF'" });

            var results = new List<DatabaseObject>();
            if (typeCodes.Count == 0 || string.IsNullOrWhiteSpace(term))
            {
                return results;
            }

            string query = $@"
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type AS TypeCode, o.object_id AS ObjectId
FROM sys.sql_modules AS m
INNER JOIN sys.objects AS o ON o.object_id = m.object_id
INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ({string.Join(",", typeCodes)})
  AND m.definition LIKE @pattern ESCAPE '\'
ORDER BY s.name, o.name;";

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@pattern", "%" + EscapeLike(term) + "%");
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string schema = reader.GetString(0);
                            string name = reader.GetString(1);
                            string typeCode = reader.GetString(2).Trim();
                            int objectId = reader.GetInt32(3);
                            results.Add(new DatabaseObject
                            {
                                Schema = schema,
                                Name = name,
                                ObjectId = objectId,
                                TypeCode = typeCode,
                                TypeLabel = LabelFor(typeCode),
                                SearchKey = TextNormalizer.Normalize($"{schema}.{name}")
                            });
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>Escapes LIKE metacharacters so the term matches literally (ESCAPE '\').</summary>
        private static string EscapeLike(string value)
            => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

        private static string LabelFor(string typeCode)
        {
            switch (typeCode)
            {
                case "U": return "Table";
                case "V": return "View";
                case "P": return "Procedure";
                case "FN":
                case "IF":
                case "TF":
                case "FS":
                case "FT":
                case "AF": return "Function";
                default: return typeCode;
            }
        }
    }
}
