namespace SsmsToolset.Data
{
    /// <summary>A schema-scoped database object (table, view, procedure, function).</summary>
    public sealed class DatabaseObject
    {
        public string Schema { get; set; }
        public string Name { get; set; }

        /// <summary>sys.objects.object_id, used to attach column/parameter lists.</summary>
        public int ObjectId { get; set; }

        /// <summary>Raw sys.objects type code (U, V, P, FN, ...).</summary>
        public string TypeCode { get; set; }

        /// <summary>Friendly group label: Table / View / Procedure / Function.</summary>
        public string TypeLabel { get; set; }

        /// <summary>
        /// Comma-separated column names (tables/views) or parameter names
        /// (procedures/functions). Populated only when the optional
        /// "Columns/Params" column is enabled.
        /// </summary>
        public string ColumnsOrParams { get; set; }

        public string FullName => $"{Schema}.{Name}";

        /// <summary>
        /// True for tables and views — the "table or equivalent" objects that
        /// SELECT / UPDATE / DELETE can target. Used to enable/disable those
        /// row-menu actions.
        /// </summary>
        public bool IsTabular => TypeCode == "U" || TypeCode == "V";

        /// <summary>Normalized (lower-cased, accent-stripped) key used for searching.</summary>
        public string SearchKey { get; set; }
    }
}
