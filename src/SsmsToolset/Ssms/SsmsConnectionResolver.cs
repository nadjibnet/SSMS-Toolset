using System;
using System.Data.SqlClient;
using System.Reflection;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;

namespace SsmsToolset.Ssms
{
    /// <summary>
    /// Turns the connection carried by a selected Object Explorer node into an
    /// ADO.NET connection string — <b>reusing SSMS's already-authenticated
    /// connection</b> so the user is never prompted to log in again.
    ///
    /// All access is via reflection so we don't need a compile-time reference to
    /// Microsoft.SqlServer.ConnectionInfo.dll (the node's <c>Connection</c> is a
    /// <c>SqlOlapConnectionInfoBase</c>, which lives there).
    /// </summary>
    public static class SsmsConnectionResolver
    {
        public static string BuildConnectionString(INodeInformation node, string databaseName)
        {
            // Read node.Connection reflectively (its declared type is in an unreferenced assembly).
            object connection = node.GetType()
                .GetProperty("Connection", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(node);

            return BuildConnectionString(connection, databaseName);
        }

        private static string BuildConnectionString(object connection, string databaseName)
        {
            if (connection == null)
            {
                return null;
            }

            try
            {
                var type = connection.GetType();

                // Path 1 (preferred): reuse the live SqlConnection SSMS already opened.
                // Its connection string is authenticated for whatever auth type is in use.
                var sqlConn = type.GetProperty("SqlConnectionObject")?.GetValue(connection) as SqlConnection;
                if (sqlConn != null)
                {
                    var builder = new SqlConnectionStringBuilder(sqlConn.ConnectionString)
                    {
                        InitialCatalog = databaseName,
                        TrustServerCertificate = true,
                        ConnectTimeout = 30
                    };

                    // SQL-auth passwords are stripped from the string; re-inject if exposed.
                    if (!builder.IntegratedSecurity)
                    {
                        string password = type.GetProperty("Password")?.GetValue(connection)?.ToString();
                        if (!string.IsNullOrEmpty(password))
                        {
                            builder.Password = password;
                        }
                    }

                    return builder.ConnectionString;
                }

                // Path 2 (fallback): assemble from individual properties.
                string server = type.GetProperty("ServerName")?.GetValue(connection)?.ToString();
                if (string.IsNullOrEmpty(server))
                {
                    return null;
                }

                bool windowsAuth = true;
                var useIntegrated = type.GetProperty("UseIntegratedSecurity");
                if (useIntegrated?.PropertyType == typeof(bool))
                {
                    windowsAuth = (bool)useIntegrated.GetValue(connection);
                }
                else
                {
                    var authType = type.GetProperty("AuthenticationType");
                    if (authType?.PropertyType == typeof(int))
                    {
                        windowsAuth = (int)authType.GetValue(connection) == 0;
                    }
                }

                if (windowsAuth)
                {
                    return $"Server={server};Database={databaseName};Integrated Security=SSPI;TrustServerCertificate=True;";
                }

                string user = type.GetProperty("UserName")?.GetValue(connection)?.ToString() ?? string.Empty;
                string pwd = type.GetProperty("Password")?.GetValue(connection)?.ToString() ?? string.Empty;
                return $"Server={server};Database={databaseName};User Id={user};Password={pwd};TrustServerCertificate=True;";
            }
            catch
            {
                return null;
            }
        }
    }
}
