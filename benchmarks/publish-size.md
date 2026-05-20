# Publish Size Benchmarks — Credfeto.Dispatcher.Server

Measured on 2026-05-17, .NET 10.0.300, linux-x64 self-contained publish.

## Results

| Configuration | Total dir size | Files | Notes |
| --- | --- | --- | --- |
| No trimming, no AOT | **115 MB** | 391 | Full .NET runtime + all assemblies |
| Trimming, no AOT | **41 MB** | 195 | IL linker removes unused code/assemblies |
| Trimming + Native AOT | ❌ Not possible | — | EF Core incompatible (see below) |

**Trimming saves 74 MB (64% reduction)** vs the untrimmed baseline.

## How measurements were taken

```bash
# Build 1: no trimming, no AOT
dotnet publish src/Credfeto.Dispatcher.Server/Credfeto.Dispatcher.Server.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishTrimmed=false -p:RunAOTCompilation=false -p:EnableTrimAnalyzer=false \
  -p:NuGetAudit=false -o /tmp/bench-1-none

# Build 2: trimming, no AOT
dotnet publish src/Credfeto.Dispatcher.Server/Credfeto.Dispatcher.Server.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishTrimmed=true -p:RunAOTCompilation=false \
  -p:NuGetAudit=false -o /tmp/bench-2-trim

# Build 3: Native AOT — FAILS (see below)
dotnet publish src/Credfeto.Dispatcher.Server/Credfeto.Dispatcher.Server.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishAot=true -p:NuGetAudit=false -o /tmp/bench-3-aot
```

## Why Native AOT is not possible

EF Core raises `IL3050` (`RequiresDynamicCodeAttribute`) errors that are fatal under Native AOT:

- `DbContext.DbContext(DbContextOptions)` — EF Core's change-tracking and query compilation use runtime code generation
- `RelationalDatabaseFacadeExtensions.MigrateAsync` — Migrations are discovered via `Assembly.GetTypes()` at runtime

These are architectural constraints in EF Core, not fixable by suppression annotations. Native AOT requires migrating away from EF Core (e.g. to Dapper or a custom SQL layer).

## What `IlcOptimizationPreference=Size` does

The `.csproj` has `<IlcOptimizationPreference>Size</IlcOptimizationPreference>`. This is an ILC (ILCompiler) setting that would instruct the Native AOT compiler to prefer smaller code over faster code. It has **no effect** on the JIT-based trimmed build — it only applies when `PublishAot=true` is used and the ILC compiler actually runs.

## What trimming does remove

Going from 391 → 195 files, trimming removes:

- ~196 assemblies that are never reachable from the entry point
- All unused types, methods, and fields within kept assemblies
- Most of the BCL that this app doesn't use

The `TrimmerRootAssembly` entries in the `.csproj` preserve: EF Core + SQLite assemblies (for reflection-based migration discovery), `Ben.Demystifier` (for Serilog stack formatting), and core BCL types.
