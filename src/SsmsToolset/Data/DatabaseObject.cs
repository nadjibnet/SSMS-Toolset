namespace SsmsToolset.Data
{
    /// <summary>A schema-scoped database object (table, view, procedure, function).</summary>
    public sealed class DatabaseObject
    {
        public string Schema { get; set; }
        public string Name { get; set; }

        /// <summary>Raw sys.objects type code (U, V, P, FN, ...).</summary>
        public string TypeCode { get; set; }

        /// <summary>Friendly group label: Table / View / Procedure / Function.</summary>
        public string TypeLabel { get; set; }

        public string FullName => $"{Schema}.{Name}";

        /// <summary>Normalized (lower-cased, accent-stripped) key used for searching.</summary>
        public string SearchKey { get; set; }
    }
}
