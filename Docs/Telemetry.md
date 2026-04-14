# CrashCatcher Telemetry

CrashCatcher records two supplemental telemetry streams in addition to the normal crash report.

## Files

Both logs live in the CrashCatcher save-data folder:

- `AppData\\LocalLow\\Ludeon Studios\\RimWorld by Ludeon Studios\\CrashCatcher\\FirstTickCrash.log`
- `AppData\\LocalLow\\Ludeon Studios\\RimWorld by Ludeon Studios\\CrashCatcher\\SaveDryRunFailure.log`

The report writers fold the live telemetry into those outputs.

## Input and menu telemetry

This stream records high-level user interaction and window transitions from startup onward.

Typical entry fields:

- timestamp
- action
- context
- detail

Examples:

- opening the main menu
- switching main tabs
- opening windows from `WindowStack`
- moving into a starting pawn screen

## Load phase telemetry

This stream records startup and loading milestones so the last visible phase can be identified before a crash.

Typical entry fields:

- timestamp
- phase
- detail

Examples:

- creating mod classes
- resolving implied defs
- reading save data
- generating world
- generating map
- generating terrain
- placing pawns
- updating long events

## Crash report layout

When a failure is latched, the report includes:

1. exception text
2. input/menu telemetry
3. load-phase telemetry
4. the last 100 calls

That keeps the user-facing timeline and the crash trail together in one place.
