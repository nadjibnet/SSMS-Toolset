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
            var lists = new Dictionary<int, StringBuilder>();

            void Append(int objectId, string member)
            {
                if (!byId.ContainsKey(objectId))
                {
                    return;
                }
                if (!lists.TryGetValue(objectId, out var sb))
                {
                    sb = new StringBuilder();
                    lists[objectId] = sb;
                }
                if (sb.Length > 0) { sb.Append(", "); }
                sb.Append(member);
            }

            if (wantColumns)
            {
                const string columnsQuery = @"
SELECT c.object_id, c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('U','V')
ORDER BY c.object_id, c.column_id;";
                using (var command = new SqlCommand(columnsQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Append(reader.GetInt32(0), reader.GetString(1));
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
                        Append(reader.GetInt32(0), reader.GetString(1));
                    }
                }
            }

            foreach (var pair in lists)
            {
                byId[pair.Key].ColumnsOrParams = pair.Value.ToString();
            }
        }

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
