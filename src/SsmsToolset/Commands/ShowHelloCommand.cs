using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SsmsToolset
{
    /// <summary>
    /// Phase 1 proof-of-life command. Adds "SSMS Toolset: Hello" under the Tools menu
    /// and shows a message box so we can confirm the extension builds, installs, and
    /// loads inside SSMS 22. Replaced by real commands in later phases.
    /// </summary>
    internal sealed class ShowHelloCommand
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService)
            {
                return;
            }

            var commandId = new CommandID(PackageGuids.CmdSet, PackageIds.ShowHelloCommand);
            commandService.AddCommand(new MenuCommand((sender, e) => Execute(package), commandId));
        }

        private static void Execute(AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                package,
                "SSMS Toolset is loaded and running inside SSMS 22.\n\nPhase 1 POC — the build → install → load pipeline works.",
                "SSMS Toolset",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
