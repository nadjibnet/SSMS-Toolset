using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SsmsToolset.UI;

// Aliases: the OLE.Interop types collide by name with System.Windows.Interop ones.
using OleMsg = Microsoft.VisualStudio.OLE.Interop.MSG;
using OleStream = Microsoft.VisualStudio.OLE.Interop.IStream;
using OleSize = Microsoft.VisualStudio.OLE.Interop.SIZE;
using OleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace SsmsToolset.ToolWindow
{
    /// <summary>
    /// Hosts the WPF <see cref="ToolsetPanelControl"/> inside a dockable VS Shell
    /// tool window.
    ///
    /// SSMS 22 (VS Shell 18) throws <c>COMException 0x8000FFFF</c> if you put WPF
    /// content on a classic <c>ToolWindowPane</c>. The reliable route is to
    /// implement <see cref="IVsWindowPane"/> and hand the shell a real child HWND
    /// created from an <see cref="HwndSource"/> whose root visual is our WPF control.
    /// </summary>
    [ComVisible(true)]
    public sealed class ToolsetWindowPane : IVsWindowPane
    {
        // Stable GUID for this tool window (persistence slot so it can be re-docked).
        private static readonly Guid ToolWindowGuid = new Guid("c8f406b2-0d35-4e9f-9021-3b4c5d6e7f81");

        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;

        // Each distinct server+database gets its own tool-window instance id, so
        // opening a second database gives a second panel rather than replacing the
        // first. Re-opening the same server+database re-focuses its existing panel.
        private static readonly Dictionary<string, uint> InstanceIds =
            new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        private static uint _nextInstanceId = 1;

        private readonly string _database;
        private readonly string _server;
        private readonly string _connectionString;
        private readonly Action<string> _openInSsmsQuery;
        private HwndSource _hwndSource;

        private ToolsetWindowPane(string database, string server, string connectionString, Action<string> openInSsmsQuery)
        {
            _database = database;
            _server = server;
            _connectionString = connectionString;
            _openInSsmsQuery = openInSsmsQuery;
        }

        private static uint InstanceIdFor(string server, string database)
        {
            string key = (server ?? string.Empty) + "\n" + (database ?? string.Empty);
            if (!InstanceIds.TryGetValue(key, out uint id))
            {
                id = _nextInstanceId++;
                InstanceIds[key] = id;
            }
            return id;
        }

        /// <summary>
        /// Opens the panel as a dockable tool window bound to the given connection.
        /// Each server+database gets its own instance; re-opening the same one just
        /// re-focuses it. Falls back to a floating WPF window if the shell rejects
        /// the tool window.
        /// </summary>
        public static void Open(string database, string server, string connectionString, Action<string> openInSsmsQuery)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            uint instanceId = InstanceIdFor(server, database);
            string caption = string.IsNullOrEmpty(server)
                ? $"SSMS Toolset — {database}"
                : $"SSMS Toolset — {database} ({server})";

            try
            {
                if (!(Package.GetGlobalService(typeof(SVsUIShell)) is IVsUIShell uiShell))
                {
                    throw new InvalidOperationException("IVsUIShell not available.");
                }

                var slot = ToolWindowGuid;

                // Already open for this server+database? Surface it instead of duplicating.
                if (ErrorHandler.Succeeded(
                        uiShell.FindToolWindowEx(0, ref slot, instanceId, out IVsWindowFrame existing))
                    && existing != null)
                {
                    existing.Show();
                    return;
                }

                var pane = new ToolsetWindowPane(database, server, connectionString, openInSsmsQuery);
                var toolGuid = ToolWindowGuid;
                var autoActivate = Guid.Empty;

                int hr = uiShell.CreateToolWindow(
                    (uint)(__VSCREATETOOLWIN.CTW_fInitNew | __VSCREATETOOLWIN.CTW_fForceCreate),
                    instanceId,
                    pane,
                    ref toolGuid,
                    ref slot,
                    ref autoActivate,
                    null,
                    caption,
                    null,
                    out IVsWindowFrame frame);

                ErrorHandler.ThrowOnFailure(hr);
                frame.Show();

                // frame.Show() invokes CreatePaneWindow synchronously, so the control exists now.
                pane.SetDockCallback(() =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    var empty = Guid.Empty;
                    frame.SetFramePos(VSSETFRAMEPOS.SFP_fDock, ref empty, 0, 0, 0, 0);
                });
            }
            catch
            {
                new System.Windows.Window
                {
                    Title = caption,
                    Width = 900,
                    Height = 640,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Content = new ToolsetPanelControl(database, server, connectionString, openInSsmsQuery)
                }.Show();
            }
        }

        // ── IVsWindowPane ───────────────────────────────────────────────────

        public int SetSite(OleServiceProvider psp) => VSConstants.S_OK;

        public int CreatePaneWindow(IntPtr hwndParent, int x, int y, int cx, int cy, out IntPtr hwnd)
        {
            var parameters = new HwndSourceParameters("SsmsToolsetPane")
            {
                ParentWindow = hwndParent,
                PositionX = x,
                PositionY = y,
                Width = cx > 0 ? cx : 900,
                Height = cy > 0 ? cy : 640,
                WindowStyle = unchecked((int)(WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS))
            };

            _hwndSource = new HwndSource(parameters)
            {
                RootVisual = new ToolsetPanelControl(_database, _server, _connectionString, _openInSsmsQuery)
            };

            hwnd = _hwndSource.Handle;
            return VSConstants.S_OK;
        }

        public int ClosePane()
        {
            _hwndSource?.Dispose();
            _hwndSource = null;
            return VSConstants.S_OK;
        }

        private void SetDockCallback(Action dockAction)
        {
            if (_hwndSource?.RootVisual is ToolsetPanelControl control)
            {
                control.DockAction = dockAction;
            }
        }

        public int GetDefaultSize(OleSize[] pSize) => VSConstants.E_NOTIMPL;
        public int SaveViewState(OleStream pStream) => VSConstants.S_OK;
        public int LoadViewState(OleStream pStream) => VSConstants.S_OK;
        public int TranslateAccelerator(OleMsg[] lpmsg) => VSConstants.S_FALSE;
    }
}
