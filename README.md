# SSMS-Toolset

An open-source extension for **SQL Server Management Studio 22** that adds an
**Azure Data Studio–style set of database tools** to the Object Explorer.

Right-click a **database** in Object Explorer → open a dockable panel (like a new
query window) that uses that database's own connection, where you can:

- 🔍 quickly **search** database objects (tables, views, stored procedures, functions)
- 📄 **script / get definition** of an object
- ▶️ **Select Top 100** / **Select Top 1000** into a new query window
- …and more tools over time (see the roadmap).

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

## Honest disclaimer

Microsoft classifies **third-party SSMS extensions as unsupported** — SSMS does
not block them from loading, but Microsoft won't investigate issues involving
them, and an SSMS update can break the interop this extension relies on. This
project is a community effort, provided as-is under the [MIT license](LICENSE).
It is not affiliated with or endorsed by Microsoft.

## License

[MIT](LICENSE) © SSMS-Toolset contributors
