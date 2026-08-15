# Contributing to SSMS-Toolset

Thanks for your interest in contributing! This project is an open-source
extension for **SQL Server Management Studio 22** that adds an Azure Data
Studio–style set of database tools to the Object Explorer.

## Ground rules

- Keep the architecture **simple and layered** (see [`ARCHITECTURE.md`](ARCHITECTURE.md)).
  SSMS/Visual Studio interop is isolated behind interfaces so the rest of the
  code stays testable and easy to read.
- Small, focused pull requests. One feature or fix per PR.
- Match the surrounding code style (`.editorconfig` is enforced).

## Prerequisites

- Windows with **SQL Server Management Studio 22** installed.
- **Visual Studio 2022 or newer** with the *Visual Studio extension development*
  workload (provides the VSSDK), **or** just the .NET / MSBuild toolchain — the
  VSIX build targets come from the `Microsoft.VSSDK.BuildTools` NuGet package so
  contributor builds are reproducible without a specific IDE version.

## SSMS interop assemblies

The extension builds against two Microsoft assemblies from your SSMS 22 install
(`SqlWorkbench.Interfaces.dll`, `SqlPackageBase.dll`). They are **not** committed
to this repo (`lib/` is git-ignored) and are never packaged into the VSIX. Copy
them locally once:

```powershell
./build/fetch-ssms-libs.ps1
```

`build.ps1` runs this automatically if `lib\Ssms22` is missing.

## Build & run

See [`README.md`](README.md) for the current, phase-accurate build, install, and
debug instructions. In short:

```powershell
# Build the VSIX
./build/build.ps1

# Install into SSMS 22 (scriptable)
./build/install.ps1

# Remove it
./build/uninstall.ps1
```

## Commit style

- Clear, imperative commit subjects (e.g. `Add database-node context menu`).
- Reference the phase where relevant (see [`docs/ROADMAP.md`](docs/ROADMAP.md)).
