using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SsmsToolset
{
    /// <summary>
    /// Entry point for the SSMS-Toolset extension.
    ///
    /// This is deliberately tiny: it registers the extension's command table and
    /// wires up commands. All SSMS-specific work lives behind the <c>Ssms/</c> seam
    /// (added in later phases); the package itself stays a thin host adapter.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(
        productName: "SSMS Toolset",
        productDetails: "Azure Data Studio-style database tools for SQL Server Management Studio 22.",
        productId: "0.1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.PackageString)]
    public sealed class ToolsetPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Phase 1 POC: a single Tools-menu command that proves we loaded.
            await ShowHelloCommand.InitializeAsync(this);
        }
    }
}
