using System;
using System.Linq;
using System.Reflection;
using EnvDTE;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using Microsoft.SqlServer.Management.UI.VSIntegration.Editors;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SsmsToolset.Ssms
{
    /// <summary>
    /// Opens a native SSMS query window connected to the selected database and
    /// pre-filled with SQL — the "New SSMS query" destination for object actions.
    ///
    /// It opens a new <b>untitled</b> query (via <c>IScriptFactory.CreateNewBlankScript</c>)
    /// connected to the node's database, then injects the SQL through the DTE text
    /// buffer. Nothing is written to disk, so the query behaves like a hand-typed
    /// one: the user is prompted for a location only if they choose to Save.
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

            // A blank (untitled) connected query — no backing file is created.
            scriptFactory.CreateNewBlankScript(ScriptType.Sql, connectionInfo, null);

            // The new script is now the active document; inject our SQL into it.
            if (!(Package.GetGlobalService(typeof(SDTE)) is DTE dte)
                || !(dte.ActiveDocument?.Object("TextDocument") is TextDocument textDocument))
            {
                throw new InvalidOperationException("Could not access the new query editor to insert the script.");
            }

            textDocument.StartPoint.CreateEditPoint().Insert(NormalizeNewLines(sql));
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
