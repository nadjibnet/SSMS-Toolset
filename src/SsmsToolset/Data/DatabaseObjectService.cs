using System.Collections.Generic;
using System.Data.SqlClient;

namespace SsmsToolset.Data
{
    /// <summary>
    /// Loads the object inventory (tables, views, procedures, functions) of a
    /// database from its catalog views, using the caller's connection string.
    /// </summary>
    public static class DatabaseObjectService
    {
        private const string ObjectsQuery = @"
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type AS TypeCode
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON o.schema_id = s.schema_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('U','V','P','FN','IF','TF','FS','FT','AF')
ORDER BY s.name, o.name;";

        public static List<DatabaseObject> Load(string connectionString)
        {
            var objects = new List<DatabaseObject>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(ObjectsQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string typeCode = reader.GetString(2).Trim();
                        string schema = reader.GetString(0);
                        string name = reader.GetString(1);
                        objects.Add(new DatabaseObject
                        {
                            Schema = schema,
                            Name = name,
                            TypeCode = typeCode,
                            TypeLabel = LabelFor(typeCode),
                            SearchKey = TextNormalizer.Normalize($"{schema}.{name}")
                        });
                    }
                }
            }

            return objects;
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
