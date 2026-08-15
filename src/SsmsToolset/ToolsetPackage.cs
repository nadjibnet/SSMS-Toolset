using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using Microsoft.VisualStudio.Shell;
using SsmsToolset.Ssms;
using SsmsToolset.ToolWindow;
using Task = System.Threading.Tasks.Task;

namespace SsmsToolset
{
    /// <summary>
    /// Entry point for the SSMS-Toolset extension.
    ///
    /// It auto-loads as soon as the Object Explorer is shown, then injects a
    /// "SSMS Toolset" item into the right-click context menu of <b>database</b>
    /// nodes. Clicking it opens a dockable panel bound to that database's own
    /// connection (see <see cref="ToolWindow.ToolsetWindowPane"/>).
    ///
    /// Everything host-specific (the Object Explorer tree, the connection) is kept
    /// in the <c>Ssms/</c> and <c>ToolWindow/</c> folders so the UI stays plain WPF.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(
        productName: "SSMS Toolset",
        productDetails: "Azure Data Studio-style database tools for SQL Server Management Studio 22.",
        productId: "0.1.2")]
    // Auto-load when the Object Explorer tool window is present (its well-known GUID).
    [ProvideAutoLoad(ObjectExplorerToolWindowGuid, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class ToolsetPackage : AsyncPackage
    {
        public const string PackageGuidString = "b7e3f5a1-9c24-4d8e-8f10-2a3b4c5d6e70";

        /// <summary>Well-known GUID of the SSMS Object Explorer tool window (auto-load trigger).</summary>
        private const string ObjectExplorerToolWindowGuid = "d114938f-591c-46cf-a785-500a82d97410";

        private const string MenuItemText = "SSMS Toolset";

        private IObjectExplorerService _objectExplorer;
        private TreeView _tree;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _objectExplorer = GetService(typeof(IObjectExplorerService)) as IObjectExplorerService;
            if (_objectExplorer == null)
            {
                return;
            }

            // The Object Explorer's WinForms TreeView is exposed only as a non-public
            // property; reflection is the established way to reach it in SSMS.
            var treeProperty = _objectExplorer.GetType().GetProperty(
                "Tree",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            _tree = treeProperty?.GetValue(_objectExplorer) as TreeView;
            if (_tree == null)
            {
                return;
            }

            // Fires just before the OE context menu is shown — our chance to add an item.
            _tree.ContextMenuStripChanged += OnContextMenuStripChanged;
        }

        private void OnContextMenuStripChanged(object sender, EventArgs e)
        {
            if (_tree?.ContextMenuStrip?.Items == null || _tree.SelectedNode == null)
            {
                return;
            }

            _objectExplorer.GetSelectedNodes(out int count, out INodeInformation[] nodes);
            if (count == 0 || nodes.Length == 0)
            {
                return;
            }

            // Only show on Database nodes. A database node's URN path ends in ".../Database".
            var node = nodes[0];
            if (string.IsNullOrEmpty(node.UrnPath) || !node.UrnPath.Contains("Database"))
            {
                return;
            }

            string database = node.InvariantName;
            string connectionString = SsmsConnectionResolver.BuildConnectionString(node, database);

            // Capture the node so the click handler can open a native SSMS query bound
            // to this database (see SsmsQueryLauncher).
            var selectedNode = node;

            var item = new ToolStripMenuItem(MenuItemText)
            {
                ForeColor = _tree.ForeColor,
                BackColor = _tree.BackColor
            };
            item.Click += (s, args) => OpenPanel(database, connectionString, selectedNode);

            _tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _tree.ContextMenuStrip.Items.Add(item);
            _tree.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        }

        private void OpenPanel(string database, string connectionString, INodeInformation node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Action<string> openInSsmsQuery = null;
            if (node != null)
            {
                openInSsmsQuery = sql => SsmsQueryLauncher.OpenNewQuery(node, sql);
            }

            ToolsetWindowPane.Open(database, connectionString, openInSsmsQuery);
        }
    }
}
