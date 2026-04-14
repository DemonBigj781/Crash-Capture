# Changelog

## Unreleased

### Added

- Auto-arm setting with early Prepatcher startup gating.
- Safer default capture profile with per-hook enable and disable toggles.
- Structured ignore fields for exception types, exception messages, and call substrings.
- Rolling call trail with configurable length.
- Save dry-run gate that distinguishes completed, invalid, and truncated save attempts.
- Suppressed-detection audit logging.
- Fail-safe quit blocking after a latched crash.
- README documentation for the current debug and safety behavior.

### Fixed

- Backup and real repo build wiring for the Prepatcher API reference.
- Auto-arm startup path so the mod can run passively when disabled.

### Notes

- CrashCatcher is intentionally invasive and is best treated as a troubleshooting tool rather than a quiet always-on gameplay mod.
- Native access violations still require dump analysis or external tooling.
