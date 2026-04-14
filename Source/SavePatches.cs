using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
[HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    public static class PreventUnpauseAfterCrash
    {
        public static bool Prefix()
        {
            return !FirstTickGuard.crashLatched;
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame))]
    public static class SaveGameGuard
    {
        public static bool Prefix(string fileName)
        {
            CallTrail.Record("save", "GameDataSaveLoader.SaveGame", fileName);
            var dryRunResult = SaveDryRunValidator.Validate(Current.Game, fileName);
            if (dryRunResult.Status != SaveDryRunStatus.CompletedAndValid)
            {
                SaveDryRunReporter.Write(fileName, dryRunResult);
                SaveDryRunNotice.Show(fileName, dryRunResult);
                return false;
            }

            return true;
        }

        public static Exception Finalizer(Exception __exception, string fileName)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    internal enum SaveDryRunStatus
    {
        CompletedAndValid,
        CompletedButInvalid,
        TruncatedBeforeCompletion
    }

    internal sealed class SaveDryRunResult
    {
        internal SaveDryRunStatus Status = SaveDryRunStatus.TruncatedBeforeCompletion;
        internal Exception Error;
        internal string DebugXml;
    }

    internal static class SaveDryRunValidator
    {
        internal static SaveDryRunResult Validate(Game game, string fileName)
        {
            var result = new SaveDryRunResult();
            var completedSerialization = false;
            CallTrail.Record("save", "CrashCatcher.SaveDryRun", $"{fileName} :: started");

            if (game == null)
            {
                result.Error = new InvalidOperationException("No active game is loaded for save validation.");
                result.Status = SaveDryRunStatus.TruncatedBeforeCompletion;
                EnsureSafeScribeState(fileName, result.Status);
                CallTrail.Record("save", "CrashCatcher.SaveDryRun", $"{fileName} :: {result.Status} :: {result.Error.Message}");
                return result;
            }

            try
            {
                result.DebugXml = Scribe.saver.DebugOutputFor(game);
                completedSerialization = true;

                if (string.IsNullOrWhiteSpace(result.DebugXml))
                {
                    result.Error = new InvalidOperationException("Dry-run save produced no XML output.");
                    result.Status = SaveDryRunStatus.TruncatedBeforeCompletion;
                    return result;
                }

                var trimmed = result.DebugXml.TrimEnd();
                if (!trimmed.EndsWith(">", StringComparison.Ordinal))
                {
                    result.Error = new InvalidDataException("Dry-run XML did not end with a closing tag marker; likely truncated before EOF.");
                    result.Status = SaveDryRunStatus.TruncatedBeforeCompletion;
                    return result;
                }

                XDocument parsedDocument;
                try
                {
                    parsedDocument = XDocument.Parse(result.DebugXml, LoadOptions.None);
                }
                catch (XmlException xmlEx)
                {
                    result.Error = xmlEx;
                    result.Status = IsLikelyTruncatedXml(xmlEx, result.DebugXml)
                        ? SaveDryRunStatus.TruncatedBeforeCompletion
                        : SaveDryRunStatus.CompletedButInvalid;
                    return result;
                }

                if (parsedDocument.Root == null)
                {
                    result.Error = new XmlException("Dry-run XML has no root element.");
                    result.Status = SaveDryRunStatus.CompletedButInvalid;
                    return result;
                }

                if (!string.Equals(parsedDocument.Root.Name.LocalName, "savegame", StringComparison.OrdinalIgnoreCase))
                {
                    result.Error = new InvalidDataException($"Dry-run root node was '{parsedDocument.Root.Name.LocalName}', expected 'savegame'.");
                    result.Status = SaveDryRunStatus.CompletedButInvalid;
                    return result;
                }

                result.Status = SaveDryRunStatus.CompletedAndValid;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex;
                result.Status = completedSerialization
                    ? SaveDryRunStatus.CompletedButInvalid
                    : SaveDryRunStatus.TruncatedBeforeCompletion;
                return result;
            }
            finally
            {
                if (result.Status == SaveDryRunStatus.CompletedAndValid && Scribe.mode != LoadSaveMode.Inactive)
                {
                    result.Status = SaveDryRunStatus.TruncatedBeforeCompletion;
                    result.Error = new InvalidOperationException("Dry-run completed but Scribe remained active, indicating an incomplete serialization teardown.");
                }

                EnsureSafeScribeState(fileName, result.Status);
                CallTrail.Record(
                    "save",
                    "CrashCatcher.SaveDryRun",
                    $"{fileName} :: {result.Status} :: {(result.Error != null ? result.Error.Message : "validated")}");
            }
        }

        private static bool IsLikelyTruncatedXml(XmlException exception, string debugXml)
        {
            if (string.IsNullOrWhiteSpace(debugXml))
            {
                return true;
            }

            var message = exception?.Message ?? string.Empty;
            if (message.IndexOf("unexpected end", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("end of file", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("root element is missing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var trimmed = debugXml.TrimEnd();
            return !trimmed.EndsWith(">", StringComparison.Ordinal);
        }

        private static void EnsureSafeScribeState(string fileName, SaveDryRunStatus status)
        {
            try
            {
                if (Scribe.mode == LoadSaveMode.Inactive)
                {
                    return;
                }

                Scribe.saver.ForceStop();
                CallTrail.Record("save", "CrashCatcher.SaveDryRun.Cleanup", $"{fileName} :: ForceStop ({status})");
            }
            catch (Exception cleanupEx)
            {
                Logger.Warning($"Dry-run cleanup failed: {cleanupEx.Message}");
                CallTrail.Record("save", "CrashCatcher.SaveDryRun.Cleanup", $"{fileName} :: cleanup failed :: {cleanupEx.Message}");
            }
        }
    }

    internal static class SaveDryRunReporter
    {
        internal static string LastReportPath { get; private set; }

        internal static void Write(string fileName, SaveDryRunResult result)
        {
            var folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher");
            Directory.CreateDirectory(folder);
            LastReportPath = Path.Combine(folder, "SaveDryRunFailure.log");

            var interpretation = result.Status == SaveDryRunStatus.TruncatedBeforeCompletion
                ? "Dry-run was cut off before completion (possible abrupt termination / missing EOF)."
                : result.Status == SaveDryRunStatus.CompletedButInvalid
                    ? "Dry-run finished but produced invalid save data."
                    : "Dry-run completed and validated.";

            var builder = new StringBuilder();
            builder.AppendLine("CrashCatcher save dry-run failed");
            builder.AppendLine("Target save: " + fileName);
            builder.AppendLine("Dry-run status: " + result.Status);
            builder.AppendLine("Interpretation: " + interpretation);
            builder.AppendLine();
            builder.AppendLine(result.Error?.ToString() ?? "No exception was captured.");
            builder.AppendLine();
            builder.AppendLine("--- Input / menu telemetry ---");
            foreach (var line in TelemetryRecorder.SnapshotInputs())
            {
                builder.AppendLine(line);
            }
            builder.AppendLine();
            builder.AppendLine("--- Load phase telemetry ---");
            foreach (var line in TelemetryRecorder.SnapshotPhases())
            {
                builder.AppendLine(line);
            }

            if (!string.IsNullOrWhiteSpace(result.DebugXml))
            {
                builder.AppendLine();
                builder.AppendLine("--- Dry-run XML ---");
                builder.AppendLine(result.DebugXml);
            }

            Logger.WriteAllText(LastReportPath, builder.ToString());
        }
    }

    internal static class SaveDryRunNotice
    {
        internal static void Show(string fileName, SaveDryRunResult result)
        {
            var stateText = result.Status == SaveDryRunStatus.TruncatedBeforeCompletion
                ? "The dry-run save was cut off before completion."
                : "The dry-run save completed but the output was invalid.";

            var text =
                $"CrashCatcher detected that '{fileName}' could not be serialized safely.\n\n" +
                $"{stateText}\n" +
                "The real save was blocked.\n" +
                "You can continue at your own discretion, but progress may remain unsaved until this is fixed.\n\n" +
                $"{result.Error?.Message ?? "No exception was captured."}";
            if (Find.TickManager != null)
            {
                Find.TickManager.Pause();
            }

            if (Find.WindowStack != null)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(text, "OK".Translate(), null, null, null, "Save verification failed"));
            }
            else
            {
                Logger.Warning(text);
            }
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.LoadGame), new Type[] { typeof(string) })]
    public static class LoadGameDataGuard
    {
        public static void Prefix(string saveFileName)
        {
            LoadPhaseTracker.Announce("Reading save data", saveFileName);
            CallTrail.Record("load", "GameDataSaveLoader.LoadGame", saveFileName);
        }

        public static Exception Finalizer(Exception __exception, string saveFileName)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Scribe), nameof(Scribe.EnterNode))]
    public static class ScribeEnterNodeGuard
    {
        public static void Prefix(string nodeName)
        {
            CallTrail.Record("save", "Scribe.EnterNode", nodeName);
        }
    }

    [HarmonyPatch(typeof(Scribe), nameof(Scribe.ExitNode))]
    public static class ScribeExitNodeGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("save", "Scribe.ExitNode");
        }
    }

}



