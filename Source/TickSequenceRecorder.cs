using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class TickSequenceRecorder
    {
        private const int CaptureDurationSeconds = 10;
        private const int MaxEntries = 4096;
        private static readonly object gate = new object();
        private static readonly List<string> entries = new List<string>();
        private static bool active;
        private static bool handedOff;
        private static DateTimeOffset startedAtUtc;
        private static DateTimeOffset deadlineUtc;
        private static string lastOutputPath;

        internal static bool IsActive
        {
            get
            {
                lock (gate)
                {
                    return active;
                }
            }
        }

        internal static bool WasHandedOff
        {
            get
            {
                lock (gate)
                {
                    return handedOff;
                }
            }
        }

        internal static string LastOutputPath
        {
            get
            {
                lock (gate)
                {
                    return lastOutputPath;
                }
            }
        }

        internal static bool Start()
        {
            if (!CrashCatcherModSettingsAccessor.EnableTickSequenceRecording)
            {
                return false;
            }

            lock (gate)
            {
                if (active)
                {
                    return false;
                }

                active = true;
                handedOff = false;
                startedAtUtc = DateTimeOffset.UtcNow;
                deadlineUtc = startedAtUtc.AddSeconds(CaptureDurationSeconds);
                entries.Clear();
                lastOutputPath = null;
                entries.Add(FormatEntry("start", "Ctrl+Shift+X", "tick sequence recording started"));
            }

            Log.Message("[CrashCatcher] Tick sequence recording started.");
            return true;
        }

        internal static void Update()
        {
            if (!IsActive)
            {
                return;
            }

            if (DateTimeOffset.UtcNow < deadlineUtcSnapshot)
            {
                return;
            }

            FinalizeCapture("completed");
        }

        internal static void MarkHandedOff(string sourceKey, Exception exception)
        {
            if (!IsActive)
            {
                return;
            }

            lock (gate)
            {
                handedOff = true;
            }

            FinalizeCapture("handed_off", sourceKey, exception);
        }

        internal static void RecordFromCallTrail(string phase, string call, string detail = null)
        {
            if (!ShouldCapturePhase(phase))
            {
                return;
            }

            lock (gate)
            {
                if (!active)
                {
                    return;
                }

                entries.Add(FormatEntry(phase, call, detail));
                Trim();
            }
        }

        private static DateTimeOffset deadlineUtcSnapshot
        {
            get
            {
                lock (gate)
                {
                    return deadlineUtc;
                }
            }
        }

        private static bool ShouldCapturePhase(string phase)
        {
            if (!CrashCatcherModSettingsAccessor.EnableTickSequenceRecording)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(phase))
            {
                return false;
            }

            if (CrashCatcherModSettingsAccessor.IncludeLiveGameLoopPhases && string.Equals(phase, "tick", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (CrashCatcherModSettingsAccessor.IncludeMapTickEvents && string.Equals(phase, "map", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (CrashCatcherModSettingsAccessor.IncludeThingComponentTickEvents && string.Equals(phase, "component", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (CrashCatcherModSettingsAccessor.IncludeLiveGameLoopPhases && string.Equals(phase, "world", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static void FinalizeCapture(string reason, string sourceKey = null, Exception exception = null)
        {
            string outputPath;
            string payload;
            string summary;
            bool copyToClipboard;

            lock (gate)
            {
                if (!active)
                {
                    return;
                }

                active = false;
                outputPath = BuildOutputPath();
                lastOutputPath = outputPath;
                copyToClipboard = CrashCatcherModSettingsAccessor.CopyTickSequenceSummaryToClipboard;
                var endAt = DateTimeOffset.UtcNow;
                var buffer = new StringBuilder();
                buffer.AppendLine("CrashCatcher Tick Sequence");
                buffer.AppendLine($"Started: {startedAtUtc:O}");
                buffer.AppendLine($"Ended: {endAt:O}");
                buffer.AppendLine($"Reason: {reason}");
                if (!string.IsNullOrWhiteSpace(sourceKey))
                {
                    buffer.AppendLine($"Source: {sourceKey}");
                }
                buffer.AppendLine($"Handed off: {handedOff}");
                if (exception != null)
                {
                    buffer.AppendLine($"Exception: {exception.GetType().FullName}: {exception.Message}");
                }
                buffer.AppendLine();
                buffer.AppendLine("--- Live game loop trace ---");
                foreach (var entry in entries)
                {
                    buffer.AppendLine(entry);
                }

                payload = buffer.ToString();
                summary = BuildSummary(reason, sourceKey);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, payload);
                Log.Message($"[CrashCatcher] Tick sequence recording saved to {outputPath}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Failed to write tick sequence recording:\n{ex}");
            }

            if (copyToClipboard)
            {
                try
                {
                    GUIUtility.systemCopyBuffer = summary;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CrashCatcher] Failed to copy tick sequence summary:\n{ex}");
                }
            }
        }

        private static string BuildSummary(string reason, string sourceKey)
        {
            var builder = new StringBuilder();
            builder.AppendLine("CrashCatcher tick sequence summary");
            builder.AppendLine($"Reason: {reason}");
            builder.AppendLine($"Handed off: {handedOff}");
            if (!string.IsNullOrWhiteSpace(sourceKey))
            {
                builder.AppendLine($"Source: {sourceKey}");
            }
            builder.AppendLine($"Entries: {entries.Count}");
            builder.AppendLine($"Saved: {lastOutputPath}");
            return builder.ToString();
        }

        private static string BuildOutputPath()
        {
            var folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher", "TickSequences");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"TickSequence_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt");
        }

        private static string FormatEntry(string phase, string call, string detail)
        {
            var elapsed = DateTimeOffset.UtcNow - startedAtUtc;
            var tick = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
            var builder = new StringBuilder();
            builder.Append('[').Append(elapsed.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture)).Append("] ");
            builder.Append("tick=").Append(tick).Append(' ');
            builder.Append('[').Append(phase).Append("] ").Append(call);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                builder.Append(" :: ").Append(detail);
            }

            return builder.ToString();
        }

        private static void Trim()
        {
            while (entries.Count > MaxEntries)
            {
                entries.RemoveAt(0);
            }
        }
    }
}
