using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using Microsoft.SqlServer.Management.UI.VSIntegration.Editors;
using Microsoft.VisualStudio.Shell;

namespace SsmsToolset.Ssms
{
    /// <summary>
    /// Opens a native SSMS query window connected to the selected database and
    /// pre-filled with SQL — the "New SSMS query" destination for object actions.
    ///
    /// This mirrors exactly what SSMS's own Object Explorer "New Query" does:
    /// build a valid <see cref="UIConnectionInfo"/> for the node (server + auth +
    /// database, taken from SSMS's connection cache) and call
    /// <c>IScriptFactory.CreateNewScript(file, connectionInfo, null)</c> — letting
    /// SSMS create the connection. Handing it a foreign SqlConnection instead
    /// produces "Cannot execute query without connection information".
    /// </summary>
    public static class SsmsQueryLauncher
    {
        public static void OpenNewQuery(object node, string sql)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(Package.GetGlobalService(typeof(IScriptFactory)) is IScriptFactory scriptFactory))
            {
                throw new InvalidOperationException("SSMS script factory is unavailable.");
            }

            UIConnectionInfo connectionInfo = GetUIConnectionWithDatabaseForNode(node);
            if (connectionInfo == null)
            {
                throw new InvalidOperationException("Could not resolve the database connection for this node.");
            }

            // CreateNewScript opens the file's contents as the query text; write our SQL
            // there with uniform CRLF endings so SSMS doesn't prompt about inconsistent
            // line endings (generated SQL and OBJECT_DEFINITION text can mix LF and CRLF).
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                "SsmsToolset_" + Guid.NewGuid().ToString("N") + ".sql");
            File.WriteAllText(tempPath, NormalizeNewLines(sql) + "\r\n", new UTF8Encoding(false));

            scriptFactory.CreateNewScript(tempPath, connectionInfo, null);
        }

        /// <summary>
        /// Invokes SSMS's own internal helper
        /// <c>ObjectExplorer.Utils.GetUIConnectionWithDatabaseForNode(INodeInformation)</c>
        /// via reflection. It returns a connection info registered with SSMS (so the
        /// editor accepts it) with the node's database already set.
        /// </summary>
        private static UIConnectionInfo GetUIConnectionWithDatabaseForNode(object node)
        {
            if (node == null)
            {
                return null;
            }

            Type utilsType = ResolveType(
                "Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer.Utils",
                node.GetType().Assembly);

            MethodInfo method = utilsType?.GetMethod(
                "GetUIConnectionWithDatabaseForNode",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(INodeInformation) },
                modifiers: null);

            return method?.Invoke(null, new[] { node }) as UIConnectionInfo;
        }

        private static string NormalizeNewLines(string text)
            => string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

        private static Type ResolveType(string fullName, Assembly preferred)
        {
            return preferred?.GetType(fullName)
                   ?? AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(fullName))
                        .FirstOrDefault(t => t != null);
        }
    }
}
