using System.Collections.Generic;

namespace SsmsToolset.Data
{
    /// <summary>
    /// One member of a "Columns/Params" list: a column (tables/views, possibly a
    /// primary or foreign key) or a parameter (procedures/functions, never a key).
    /// </summary>
    public sealed class ColumnToken
    {
        public string Name { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }

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

        /// <summary>
        /// Structured form of <see cref="ColumnsOrParams"/> — one token per column
        /// or parameter, carrying primary/foreign-key flags so the grid can mark
        /// and emphasise keys. Populated alongside <see cref="ColumnsOrParams"/>.
        /// </summary>
        public List<ColumnToken> ColumnTokens { get; set; }

        public string FullName => $"{Schema}.{Name}";

        /// <summary>
        /// True for tables and views — the "table or equivalent" objects that
        /// SELECT / UPDATE / DELETE can target. Used to enable/disable those
        /// row-menu actions.
        /// </summary>
        public bool IsTabular => TypeCode == "U" || TypeCode == "V";

        /// <summary>
        /// True for procedures and functions — objects that can be invoked, so the
        /// "Execute (with parameters)" action applies.
        /// </summary>
        public bool IsExecutable =>
            TypeCode == "P" || TypeCode == "FN" || TypeCode == "IF" ||
            TypeCode == "TF" || TypeCode == "FS" || TypeCode == "FT" || TypeCode == "AF";

        /// <summary>Normalized (lower-cased, accent-stripped) key used for searching.</summary>
        public string SearchKey { get; set; }
    }
}
