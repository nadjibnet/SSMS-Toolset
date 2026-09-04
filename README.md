# SSMS-Toolset

**Latest release: [v0.2.1](https://github.com/nadjibnet/SSMS-Toolset/releases/tag/0.2.1)** &nbsp;·&nbsp; [⬇ Download VSIX](https://github.com/nadjibnet/SSMS-Toolset/releases/download/0.2.1/SsmsToolset_0.2.1.vsix)

An open-source extension for **SQL Server Management Studio 22** that adds an
**Azure Data Studio–style set of database tools** to the Object Explorer.

Right-click a **database** in Object Explorer → open a dockable panel (like a new
query window) that uses that database's own connection, where you can:

- 🔍 quickly **search** database objects (tables, views, stored procedures, functions)
- 📄 **script / get definition** of an object
- ▶️ **Select Top 100** / **Select Top 1000** into a new query window
- …and more tools over time (see the roadmap).

## Screenshots

<sub>Click any image to view it full size.</sub>

<table>
  <tr>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/objects.png"><img src="docs/screenshots/objects.png" width="420" alt="Objects browser"></a><br>
      <sub><b>Objects browser</b> — search, type filters, and the Columns/Params column with <code>[pk]</code>/<code>[fk]</code> markers</sub>
    </td>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/full-definition.png"><img src="docs/screenshots/full-definition.png" width="420" alt="Full definition (sp_help)"></a><br>
      <sub><b>Full definition</b> — <code>sp_help</code> result sets shown as titled, copyable cards</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/query.png"><img src="docs/screenshots/query.png" width="420" alt="Query tab"></a><br>
      <sub><b>Query tab</b> — run SQL with timing, then Copy or Export CSV</sub>
    </td>
    <td width="50%" align="center" valign="top">
      <a href="docs/screenshots/actions.png"><img src="docs/screenshots/actions.png" width="420" alt="Row actions"></a><br>
      <sub><b>Row actions</b> — Select Top, Update/Delete, Execute, Script as CREATE, Full definition</sub>
    </td>
  </tr>
</table>

> **Status:** early development. See [`docs/ROADMAP.md`](docs/ROADMAP.md) for the
> phase-by-phase plan. We're at **Phase 0 (foundation)**.

---

## Requirements

- Windows
- **SQL Server Management Studio 22** (built on the Visual Studio 2026 shell)
- To build from source: **Visual Studio 2022/2026 with the "Visual Studio
  extension development" workload**, or just an MSBuild/.NET toolchain (the VSIX
  build targets come from the `Microsoft.VSSDK.BuildTools` NuGet package).

## Install (once releases exist)

Extensions are installed by copying the extension folder into SSMS's per-user
Extensions directory and restarting SSMS:

```powershell
# from a build or release payload
./build/install.ps1      # copies into %LocalAppData%\Microsoft\SSMS\<ver>\Extensions\SsmsToolset
./build/uninstall.ps1    # removes it
```

Then restart SSMS 22. If you downloaded the package from the internet, **Unblock**
the zip (file → Properties → Unblock) before extracting, or the shell may refuse
to load the assemblies.

> `VSIXInstaller.exe` is **not** a reliable way to install SSMS extensions — the
> folder-copy above is the supported community approach.

## Build from source

```powershell
./build/build.ps1        # restores + builds the VSIX into ./artifacts
```

See [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Security & privacy

This extension is designed to keep your credentials and data where SSMS already
keeps them:

- **Connection is inherited from SSMS.** The panel reuses the connection SSMS has
  already authenticated for the database you right-clicked — you are never prompted
  for credentials again, and the tool does not manage its own login.
- **Nothing sensitive is persisted.** The connection string (including a SQL-auth
  password, when that auth mode is in use) is held **in memory only**, for the life
  of the panel. The only file the tool writes to `%LocalAppData%\SsmsToolset\` is
  `settings.ini`, which stores **UI preferences only** (theme, query target, column
  toggles) — never connection or credential data.
- **No logging.** The tool has no log files and writes no diagnostic/telemetry data,
  so credentials and query contents cannot leak through logs.
- **`TrustServerCertificate` is enabled** for the tool's own reconnection to the
  database, matching typical SSMS usage. This trusts the server certificate without
  chain validation; it reconnects only to the same server SSMS already trusts.
- **No temp files.** Opening generated SQL in a *new SSMS query* creates an
  untitled in-memory query and injects the text directly — nothing is written to
  disk unless you choose to Save.
- **Exports are plaintext.** *Copy* and *Export CSV* on the Query tab write the
  result rows you chose to export as plaintext (clipboard / `.csv`). Treat those
  outputs as sensitive if the underlying data is.

## Honest disclaimer

Microsoft classifies **third-party SSMS extensions as unsupported** — SSMS does
not block them from loading, but Microsoft won't investigate issues involving
them, and an SSMS update can break the interop this extension relies on. This
project is a community effort, provided as-is under the [MIT license](LICENSE).
It is not affiliated with or endorsed by Microsoft.

## License

[MIT](LICENSE) © SSMS-Toolset contributors
