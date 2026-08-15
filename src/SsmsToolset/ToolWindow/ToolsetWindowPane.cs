using System;
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

        private readonly string _database;
        private readonly string _connectionString;
        private readonly Action<string> _openInSsmsQuery;
        private HwndSource _hwndSource;

        private ToolsetWindowPane(string database, string connectionString, Action<string> openInSsmsQuery)
        {
            _database = database;
            _connectionString = connectionString;
            _openInSsmsQuery = openInSsmsQuery;
        }

        /// <summary>
        /// Opens the panel as a dockable tool window bound to the given connection.
        /// Falls back to a floating WPF window if the shell rejects the tool window.
        /// </summary>
        public static void Open(string database, string connectionString, Action<string> openInSsmsQuery)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (!(Package.GetGlobalService(typeof(SVsUIShell)) is IVsUIShell uiShell))
                {
                    throw new InvalidOperationException("IVsUIShell not available.");
                }

                var pane = new ToolsetWindowPane(database, connectionString, openInSsmsQuery);
                var toolGuid = ToolWindowGuid;
                var autoActivate = Guid.Empty;

                int hr = uiShell.CreateToolWindow(
                    (uint)(__VSCREATETOOLWIN.CTW_fInitNew | __VSCREATETOOLWIN.CTW_fForceCreate),
                    0,
                    pane,
                    ref toolGuid,
                    ref toolGuid,
                    ref autoActivate,
                    null,
                    $"SSMS Toolset — {database}",
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
                    Title = $"SSMS Toolset — {database}",
                    Width = 900,
                    Height = 640,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Content = new ToolsetPanelControl(database, connectionString, openInSsmsQuery)
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
                RootVisual = new ToolsetPanelControl(_database, _connectionString, _openInSsmsQuery)
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
