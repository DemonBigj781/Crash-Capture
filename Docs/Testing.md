# CrashCatcher Testing

## First checks

1. Verify the mod loads without startup exceptions.
2. Confirm the Auto-arm setting is persisted.
3. Confirm the rolling call count setting is written to disk.
4. Confirm the report folder is created after a latched failure.

## Save dry-run tests

- Trigger a normal save and confirm the dry-run path stays quiet.
- Trigger a known-bad save path and confirm the result is reported as:
  - `CompletedButInvalid`
  - or `TruncatedBeforeCompletion`
- Confirm the real save is blocked when the dry-run fails.

## Hook filter tests

- Disable a detector and confirm it no longer latches.
- Add an ignore rule and confirm the suppressed event is recorded in the audit log instead of freezing the game.
- Use Restore Defaults and verify all values return to the conservative profile.

## Auto-arm tests

- Disable Auto-arm and restart the game.
- Confirm CrashCatcher logs the passive startup path.
- Re-enable Auto-arm and restart again.
- Confirm the bootstrap activates early.

## Notes

- For native access violations, use dump analysis in addition to CrashCatcher logs.
- If the report trail does not populate, confirm the detector responsible for the failure is enabled.
