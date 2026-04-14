# CrashCatcher Architecture

## Overview

CrashCatcher is split into a managed RimWorld mod and a Prepatcher bootstrap path.

### Managed mod

The managed mod owns:

- settings UI
- hook filters
- rolling call trail
- save dry-run validation
- report generation
- fail-safe pause and quit behavior
- Unity log and managed exception capture

### Prepatch bootstrap

The Prepatcher bootstrap runs early so CrashCatcher can arm before the normal Harmony reload completes.

It currently:

- probes the saved Auto-arm setting
- skips hook install if auto-arm is disabled
- installs the hook layer if auto-arm is enabled or the setting cannot be read yet

## Important files

- `Source/Main.cs`
- `Source/Prepatching/FreePatchEntry.cs`
- `README.md`
- `CHANGELOG.md`

## Runtime behavior

When enabled, CrashCatcher:

1. Arms hooks at startup.
2. Tracks call trail entries.
3. Intercepts managed failures and save validation issues.
4. Freezes and reports instead of letting the failure continue silently.

## Current constraints

- Native access violations still require dump analysis.
- The mod is intentionally invasive and should be treated as a debug aid.
