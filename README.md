# CrashCatcher

CrashCatcher is a RimWorld debug mod for catching serious failures before they turn into a full desktop crash. It is designed for troubleshooting heavily modded setups where a normal exception trace is not enough to understand what happened.

## What it does

- Hooks early through Prepatcher so it can arm before the normal Harmony reload completes.
- Watches a broad set of load, tick, render, save, and lifecycle entry points.
- Keeps a rolling call trail so the last few calls before a failure are preserved.
- Writes a dedicated crash report to RimWorld's save-data folder.
- Holds the game paused when a serious failure is detected instead of letting the failure continue without context.
- Includes a fail-safe that blocks unexpected termination once the catcher has latched a crash.

## Current scope

CrashCatcher is intentionally broad because it is meant to help diagnose crashes that happen:

- during startup
- during world load
- during save and load processing
- during the first active world tick after load
- during render and texture loading
- during entity, component, and world-object lifecycle methods
- during Unity's threaded log and unhandled exception paths

It also includes a save dry-run gate that checks whether the save pipeline can complete cleanly before the real save is written.

## Safety profile

The mod is useful as a debug tool, but it is invasive by design. It should be treated as a troubleshooting aid rather than a quiet always-on gameplay mod.

By default, CrashCatcher uses a safer, conservative profile (fewer detectors enabled). You can opt into broader capture using the per-hook toggles.

To reduce noise, the mod includes:

- per-hook enable and disable toggles
- ignore lists for exception types, exception messages, and call substrings
- a restore-defaults button in the settings UI
- a configurable rolling call buffer

## Load phase announcements

CrashCatcher logs startup and load phases as they happen so you can see what the game is doing before the world fully appears.

During those phases it will announce stages like:

- creating mod classes
- resolving implied defs
- reading save data
- loading game data
- updating long events
- finalizing game initialization

Those announcements are recorded in the call trail and also written to the log so you can follow the pre-load sequence without waiting for the first map or crash.

## Settings

CrashCatcher exposes the following settings in-game:

- `Auto-arm CrashCatcher`
  - When enabled, CrashCatcher installs its runtime hooks automatically on load (including fail-safe behavior).
  - When disabled, CrashCatcher loads passively and does not install hooks. This is useful when you want the mod present but inert.
  - Note: changing this requires a restart to fully take effect, because hook installation happens during startup.

- `Rolling call count`
  - Controls how many recent calls are kept in the report trail
  - Default: `100`
  - Clamped between `10` and `1000`

- Individual hook toggles
  - Each detector can be enabled or disabled separately
  - The labels in the UI are friendly instead of raw internal method names

- Ignore lists
  - Ignored exception types
  - Ignored exception messages
  - Ignored call substrings

- Restore defaults
  - Resets the call count, detector toggles, and ignore fields back to the safer default profile

## Save dry-run gate

CrashCatcher includes a save preflight path that tries to validate the save structure before the real save is written.

The dry-run gate distinguishes three outcomes:

- `CompletedAndValid`
  - The save structure completed and the output is structurally valid

- `CompletedButInvalid`
  - The save process completed, but the resulting structure failed validation

- `TruncatedBeforeCompletion`
  - The save stopped early or failed before the save stream reached a clean end state

This is meant to catch the kind of failure where a save dies partway through and leaves behind an incomplete file or a cut-off XML tree.

## Crash report contents

When CrashCatcher latches a serious failure, it writes a report file under:

- `AppData\\LocalLow\\Ludeon Studios\\RimWorld by Ludeon Studios\\CrashCatcher\\FirstTickCrash.log`

The report includes:

- the exception text
- the rolling call trail
- save-related breadcrumbs
- the current crash state context

When the fail-safe is working, the in-game notice stays up with the crash summary and the report path visible so you can capture or review it before exiting. A typical example is the texture-compression failure notice shown in the documented screenshot from the current test session.

Suppressed detections are also written to a separate audit log so you can see what was ignored or disabled later.

The telemetry streams that feed those reports are kept in the same CrashCatcher folder and are folded into the crash output:

- input/menu telemetry
- load-phase telemetry

That gives you both the last user actions and the last load milestones alongside the crash trail instead of forcing you to reconstruct the timeline from the main log alone.

## Notice screenshot

The fail-safe notice is intended to stay on screen after a serious failure so the crash summary remains visible. In the current test session, the notice showed:

- the trapped exception message
- the report path
- the `Open report folder` and `Close game` actions

That layout is deliberate: it keeps the failure visible and gives you a direct exit path without dismissing the evidence first.

## Native crash caveat

CrashCatcher can catch managed exceptions and Unity log failures, but it cannot guarantee recovery from a true native crash or access violation below the managed layer. If RimWorld terminates in `ntdll.dll` or another native module, that usually means the failure is outside the mod's direct control.

## Recommended use

Best use cases:

- narrowing down a mod conflict
- finding where a save fails
- capturing a first-tick or startup failure
- confirming whether a warning is harmless or actually part of a crash path

Less ideal as:

- a permanent gameplay mod for casual play
- a substitute for fixing the real underlying crash source

## Dependencies

CrashCatcher is built for RimWorld 1.6 and loads after Prepatcher while remaining before Harmony in mod order.

## Notes

- The mod is intentionally verbose when it is active.
- The fail-safe is designed to preserve context and keep the failure visible instead of letting the game silently continue into a worse state.
- If you are using CrashCatcher in a live test session, keep an eye on the settings you enable so you only capture the detectors you actually want to probe.
