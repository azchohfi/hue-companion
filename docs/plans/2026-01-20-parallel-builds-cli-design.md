# Parallel Builds CLI Design

## Problem

When developing, there's no easy way to:
1. Compare current changes against a known-good reference (before vs after)
2. Keep a working version running while fixing bugs in dev

## Solution

A CLI tool (`hue.cmd`) that manages two build outputs from one codebase:
- **Dev build**: Fast incremental builds for active development
- **Stable build**: Self-contained checkpoint you cut at milestones

Both can run simultaneously with different MSIX identities.

## Build Architecture

```
hue-companion/
├── src/                      # Source code (unchanged)
├── builds/
│   └── stable/               # Stable build output (gitignored)
│       ├── HueCompanion.exe    # Self-contained publish
│       └── build-info.json   # Metadata about this cut
├── bin/                      # Dev build output (existing)
├── hue.cmd                   # CLI entry point
└── tools/
    └── hue-cli.ps1           # CLI implementation
```

### Identity Separation

| Property | Dev Build | Stable Build |
|----------|-----------|--------------|
| Title | Hue Companion (Dev) | Hue Companion |
| Package Name | HueCompanion.Dev | HueCompanion |
| Output Path | `src/HueCompanion/bin/...` | `builds/stable/` |

### Versioning

Derived from base version in .csproj with automatic suffixes:

| Build Type | Format | Example |
|------------|--------|---------|
| Public release | `X.Y.Z` | `0.2.0` |
| Stable | `X.Y.Z-stable.YYYYMMDD` | `0.2.0-stable.20260120` |
| Dev | `X.Y.Z-dev+<hash>` | `0.2.0-dev+f6bcc34` |

Version displays in window title bar for easy identification.

## CLI Commands

```
.\hue dev              # Build and launch dev version
.\hue stable           # Launch existing stable (no build)
.\hue cut              # Build current code as new stable
.\hue list             # Show running Hue instances
.\hue kill dev         # Kill dev instances
```

### Command Details

**`hue dev`**
- Runs `dotnet build` with `-p:DevBuild=true`
- Injects version suffix with current git hash
- Launches the exe
- Flag: `--no-launch` to build without launching
- Flag: `--restart` to kill existing dev instances first

**`hue stable`**
- Launches existing stable exe from `builds/stable/`
- Errors if no stable build exists
- No build step - instant launch

**`hue cut`**
- Runs `dotnet publish` (self-contained) to `builds/stable/`
- Injects version suffix with today's date
- Warns if uncommitted changes exist
- Flag: `--message "description"` to log why you cut it
- Writes `build-info.json` with metadata

**`hue list`**
- Shows running Hue processes with version and type (dev/stable)

**`hue kill dev`**
- Kills all dev instances, leaves stable running

## Implementation Details

### MSBuild Properties

In `HueCompanion.csproj`:

```xml
<PropertyGroup Condition="'$(DevBuild)' == 'true'">
  <ApplicationTitle>Hue Companion (Dev)</ApplicationTitle>
  <PackageName>HueCompanion.Dev</PackageName>
</PropertyGroup>

<PropertyGroup Condition="'$(DevBuild)' != 'true'">
  <ApplicationTitle>Hue Companion</ApplicationTitle>
  <PackageName>HueCompanion</PackageName>
</PropertyGroup>
```

### Version in Title Bar

Main window title format: `{ApplicationTitle} - {Version}`

Example: `Hue Companion (Dev) - 0.1.0-dev+f6bcc34`

### Stable Build Metadata

`builds/stable/build-info.json`:

```json
{
  "version": "0.1.0-stable.20260120",
  "cutAt": "2026-01-20T14:30:00Z",
  "gitHash": "f6bcc34",
  "message": "Before refactoring navigation"
}
```

## Edge Cases

### First-time Setup
- `hue dev` works immediately
- `hue stable` errors: "No stable build exists. Run `hue cut` first."
- `hue cut` creates `builds/stable/` directory if needed

### Dirty Git State
- Dev builds work with uncommitted changes
- `hue cut` warns: "Working directory has uncommitted changes. Cut anyway? [y/N]"

### Already Running
- Commands don't kill existing instances by default
- Use `--restart` flag with `hue dev` to kill and relaunch

### Build Failures
- CLI shows error output and exits
- No partial state - stable folder only updates on success

## Gitignore

Add to `.gitignore`:

```
builds/
```
