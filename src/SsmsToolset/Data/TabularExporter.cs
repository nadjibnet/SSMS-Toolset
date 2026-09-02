using System;
using System.Data;
using System.Globalization;
using System.Text;

namespace SsmsToolset.Data
{
    /// <summary>
    /// Serializes a <see cref="DataTable"/> to delimited text (CSV for files,
    /// TSV for the clipboard) with RFC-4180-style quoting so values containing the
    /// delimiter, quotes, or line breaks survive a round-trip.
    /// </summary>
    public static class TabularExporter
    {
        public static string ToDelimited(DataTable table, string delimiter)
        {
            var sb = new StringBuilder();

            for (int c = 0; c < table.Columns.Count; c++)
            {
                if (c > 0) { sb.Append(delimiter); }
                sb.Append(Escape(table.Columns[c].ColumnName, delimiter));
            }
            sb.Append("\r\n");

            foreach (DataRow row in table.Rows)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    if (c > 0) { sb.Append(delimiter); }
                    object value = row[c];
                    string text = value == null || value == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                    sb.Append(Escape(text, delimiter));
                }
                sb.Append("\r\n");
            }

            return sb.ToString();
        }

        private static string Escape(string value, string delimiter)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            bool needsQuoting =
                value.IndexOf(delimiter, StringComparison.Ordinal) >= 0 ||
                value.IndexOf('"') >= 0 ||
                value.IndexOf('\n') >= 0 ||
                value.IndexOf('\r') >= 0;

            return needsQuoting
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
