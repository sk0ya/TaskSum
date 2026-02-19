# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build (Debug)
dotnet build

# Run
dotnet run

# Build Release
dotnet build -c Release
```

This is a Windows-only WPF application (`net8.0-windows`). It cannot be built or run on non-Windows platforms.

## Architecture

TaskSum is a WPF desktop app that fetches Azure DevOps work items under a given Feature ID and displays them in a tree view with effort aggregation. It follows the MVVM pattern.

**Data flow:**
1. User enters Organization URL, Project name, and Feature ID
2. `MainViewModel` calls `CredentialManagerService.GetPat()` to retrieve the PAT from Windows Credential Manager (credential name: `ADO_PAT`)
3. `AdoService` fetches the full hierarchy via a WIQL recursive query (`GetDescendantLinksAsync`), then batch-fetches work item details 200 at a time (`GetWorkItemsAsync`)
4. `MainViewModel` builds `WorkItemNodeViewModel` tree nodes, assigns parent/child relationships and depth levels, and populates `VisibleNodes` (a flat list representing the visible tree rows)
5. Filters (AssignedTo, State) are applied by rebuilding `VisibleNodes` — nodes matching the filter or having matching descendants are included; collapsed nodes hide their children
6. `AggregationItems` aggregates effort fields (OriginalEstimate, RemainingWork, CompletedWork) grouped by Activity for the currently visible/filtered nodes

**Key files:**
- `Services/AdoService.cs` — ADO REST API calls (WIQL + work item batch fetch)
- `Services/CredentialManagerService.cs` — P/Invoke into `Advapi32.dll` to read from Windows Credential Manager
- `Services/SettingsService.cs` — Persists OrganizationUrl and Project to `%APPDATA%\TaskSum\settings.json`
- `ViewModels/MainViewModel.cs` — All application logic: loading, tree building, filtering, aggregation
- `ViewModels/WorkItemNodeViewModel.cs` — Tree node wrapping `WorkItemData` with Level, IsExpanded, Children, Parent
- `Models/WorkItemData.cs` — Plain data record for a fetched work item
- `Models/AggregationItem.cs` — Aggregation row model (per-Activity totals + a grand total row with `IsTotal=true`)
- `MainWindow.xaml` — Single-window UI: settings bar → filter bar → ListView (tree) → DataGrid (aggregation) → StatusBar
- `Commands/RelayCommand.cs` — `RelayCommand`, `RelayCommand<T>`, and `AsyncRelayCommand` implementations

**Tree rendering approach:** The tree is rendered as a flat `ListView` (not a `TreeView`). `VisibleNodes` contains only the currently visible rows. Indentation is applied via `LevelToIndentConverter` using the node's `Level` property. Expand/collapse rebuilds `VisibleNodes` from `_rootNodes`.

## PAT Setup

To run the app, a Personal Access Token must be stored in Windows Credential Manager:
- Type: Generic credential
- Name: `ADO_PAT`
- Password: the PAT value

The PAT needs read access to Azure DevOps Work Items.
