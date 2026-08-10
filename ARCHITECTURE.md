# Architecture

SSMS-Toolset is a **Visual Studio–style VSIX extension** that loads inside
**SQL Server Management Studio 22**. The design goal is *boring and layered*: the
messy SSMS/Visual Studio interop is quarantined behind small interfaces so the
rest of the code is plain, testable C# that an open-source contributor can read.

## The host: what SSMS 22 actually is

- SSMS 22 is built on the **Visual Studio 2026 (18.x) isolated shell**, 64-bit
  (SSMS 21 was VS 2022 / 17.x — a *different* shell generation). We target
  **SSMS 22 only** to keep things simple.
- Extensions are **VSIX** packages containing a **`.NET Framework 4.7.2`**
  assembly, built **AnyCPU** (`Prefer32Bit=false`) so they load under both x64
  and Windows Arm64 CLRs.
- ⚠️ Microsoft classifies third-party SSMS extensions as **unsupported** — SSMS
  does not block them from loading, but there is no official support or API doc.
  We therefore isolate every host dependency and expect to adapt across SSMS
  updates. See the honesty note in the README.

## Layers

```
+-----------------------------------------------------------+
|  SsmsToolset  (VSIX package, .NET Framework 4.7.2)         |
|                                                           |
|  - ToolsetPackage : AsyncPackage        (entry point)     |
|  - Commands (.vsct)                      (menu items)     |
|  - ToolWindow : ToolWindowPane           (dockable host)  |
|  - Ssms/  ISsmsContext + SsmsContext     <-- HOST GLUE     |
+------------------------|----------------------------------+
                         | ISsmsContext (interface)
                         v
+-----------------------------------------------------------+
|  SsmsToolset.UI    (WPF + MVVM UserControls, ViewModels)  |
|    - no direct SSMS/VS types; talks to ISsmsContext        |
+------------------------|----------------------------------+
                         | ISchemaService / model types
                         v
+-----------------------------------------------------------+
|  SsmsToolset.Core  (pure C#, no VS/SSMS references)       |
|    - DB object model (tables/views/procs/functions)       |
|    - SQL generation (SELECT TOP N, scripting)             |
|    - data access over SqlConnection / SMO                 |
|    - unit-testable in isolation                           |
+-----------------------------------------------------------+
```

Early phases keep everything in the single `SsmsToolset` project; `UI` and
`Core` are split out as they grow. The **only** rule that never bends: `Core`
and `UI` never reference SSMS/VS assemblies — they go through `ISsmsContext`.

## The host-glue seam (`ISsmsContext`)

Everything SSMS-specific hides behind one interface, implemented once against the
real interop and faked in tests:

```csharp
public interface ISsmsContext
{
    // The Object Explorer node the user right-clicked, if any.
    SelectedDatabase? GetSelectedDatabase();

    // Open a "New Query" editor bound to a database's connection (SSMS-style),
    // optionally pre-filled with SQL.
    void OpenNewQuery(SelectedDatabase target, string sql, bool execute);
}

public sealed record SelectedDatabase(
    string Server, string Database, System.Data.IDbConnection Connection, object UiConnectionInfo);
```

### What it maps to in the SSMS interop

All of these live in **`SqlWorkbench.Interfaces.dll`**, referenced directly from
the SSMS install directory (they are **not** on NuGet). We vendor a copy under
`lib/Ssms22/` and reference it, with `Private=false` so we don't redistribute it.

| Concept | SSMS interop API |
| --- | --- |
| Get selected OE nodes | `IObjectExplorerService.GetSelectedNodes(out size, out INodeInformation[])` |
| Is it a database node? | parse `INodeInformation.Context` URN → last part is `Database[@Name='...']` |
| Its connection | `INodeInformation.Connection` (`IDbConnection`) + `UIConnectionInfo` |
| Open New Query on it | `ServiceCache.ScriptFactory.CreateNewBlankScript(ScriptType.Sql, uiConnectionInfo, dbConnection)` |
| Dockable panel | `ToolWindowPane` whose `Content` is a WPF `UserControl` |

> The exact signatures in `SqlWorkbench.Interfaces.dll` are **not documented by
> Microsoft** for SSMS 22. They are verified empirically against the shipped DLL
> (via decompiler / reflection) as each phase lands, and pinned in `Ssms/`.

## Data access (`Core`)

`Core` talks to the database with the `IDbConnection` handed to it — no separate
login. Object discovery and search use catalog views (`sys.objects`,
`sys.schemas`, …); scripting/definition uses `OBJECT_DEFINITION` /
`sp_helptext` or **SMO** (`Microsoft.SqlServer.SqlManagementObjects`, which *is*
on NuGet). SQL generation (e.g. `SELECT TOP 100 ... FROM [schema].[obj]`) is pure
string logic and fully unit-tested.

## Build & packaging

- Legacy-format VSIX project (`ProjectTypeGuids`) targeting `net472`, AnyCPU
  (`Prefer32Bit=false`). VS reference assemblies come from NuGet
  (`Microsoft.VisualStudio.SDK`, `Microsoft.VSSDK.BuildTools`); the VSIX
  packaging targets come from the installed VSSDK (`Microsoft.VsSDK.targets`).
  Building therefore needs MSBuild from a VS install that has the *Visual Studio
  extension development* (VSSDK) component — VS 2022 (17.x) or VS 2026 (18.x).
- Output is a `.vsix` (a zip). Install = unpack its payload into the SSMS
  per-user Extensions folder; see the README and `build/` scripts.

## Testing strategy

- `Core` → plain unit tests (xUnit), no host needed.
- `UI` → ViewModel tests against a fake `ISsmsContext`.
- Host glue → validated manually inside SSMS 22 at each phase checkpoint (there
  is no headless way to test the real Object Explorer interop).
