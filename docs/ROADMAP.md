# SSMS-Toolset — Phased Roadmap

The project is built in small, independently testable phases. Each phase ends
with a commit and a **manual validation checkpoint** so you can confirm it works
in SSMS 22 before we build on top of it.

Legend: ☐ not started · ◐ in progress · ☑ done & validated

---

## Phase 0 — Repository foundation  ◐
Repo hygiene and the plan itself. Nothing to run yet.

- License (MIT), `.gitignore`, `.editorconfig`
- `README.md`, `ARCHITECTURE.md`, `CONTRIBUTING.md`, this roadmap
- **Validation:** review the plan & architecture.

## Phase 1 — POC: an extension that loads in SSMS 22  ☐
Prove the full build → package → install → load pipeline.

- Minimal VSIX (`AsyncPackage`) with one command under the **Tools** menu that
  shows a message box.
- `build/build.ps1`, `build/install.ps1`, `build/uninstall.ps1`.
- **Validation:** install into SSMS 22, see the menu item, click it, see the box.

## Phase 2 — Context menu on **database nodes only**  ☐
Hook the Object Explorer.

- `.vsct` command placed in the database-node context menu, filtered so it only
  appears on database nodes (not servers, folders, tables, …).
- Read the selected node and prove detection by showing
  `Server / Database` of the clicked node.
- **Validation:** right-click a database → the item appears; right-click a table
  or the server → it does **not**.

## Phase 3 — Dockable WPF tool window  ☐
The ADS-style panel shell.

- `ToolWindowPane` hosting a WPF `UserControl` (MVVM). Opens dockable next to the
  query editor, like a new query window.
- Static UI only (header, search box, tree placeholder).
- **Validation:** menu item opens a dockable, re-dockable, closable panel.

## Phase 4 — Use the selected database's connection  ☐
Wire real context into the panel.

- Capture the selected node's connection (server, database, auth) and display the
  live identity in the panel header.
- Open a **New Query** window bound to that same connection (the SSMS way).
- **Validation:** panel shows the correct server/db; "New Query" opens already
  connected to that database.

## Phase 5 — Core tools (Azure Data Studio "manage"-style)  ☐
The actual value. Each tool is its own small commit.

- Object list + **search** (tables, views, stored procedures, functions).
- Per-object actions:
  - **Select Top 100** / **Select Top 1000** → new query window, executed.
  - **Script as CREATE / definition** (get definition).
  - (later) Edit Top 200, script DROP, properties, etc.
- **Validation:** search finds objects; each action produces correct SQL against
  the selected database's connection.

## Phase 6 — Packaging, installer & CI  ☐
Make it easy for others to install.

- Polished scriptable install/uninstall (already scaffolded in Phase 1). Install
  = **copy the extension folder** into the SSMS per-user Extensions directory
  (`%LocalAppData%\Microsoft\SSMS\<version>\Extensions\SsmsToolset\`) and restart
  SSMS. (`VSIXInstaller.exe` is *not* a reliable path for SSMS extensions.)
- A double-click **installer** (WiX or Inno Setup) that unpacks the VSIX payload
  into that Extensions folder and offers uninstall.
- GitHub Actions: build the VSIX and attach it to releases.
- **Validation:** a fresh machine can install from a release artifact.

---

## Non-goals (for now)
- Supporting SSMS 20 / 21 (they are on a different shell version). Targeting
  SSMS 22 keeps the model simple; multi-version support can come later.
- Replacing SSMS features that already work well.
