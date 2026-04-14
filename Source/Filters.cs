using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class CrashCatcherFilters
    {
        private static CrashCatcherSettings settings;
        private static readonly object auditGate = new object();
        private static readonly string auditFolder = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher");
        private static readonly string auditPath = Path.Combine(auditFolder, "CrashCatcherAudit.log");
        private static readonly string[] explicitKeys =
        {
            "FirstTickGuard",
            "TickManagerUpdateGuard",
            "GameUpdateEntryGuard",
            "GameUpdatePlayGuard",
            "MapUpdateGuard",
            "MapPreTickGuard",
            "MapPostTickGuard",
            "GameComponentFinalizeInitGuard",
            "GameComponentStartedNewGameGuard",
            "GameComponentLoadedGameGuard",
            "WorldComponentTickGuard",
            "WorldComponentUpdateGuard",
            "WorldComponentFinalizeInitGuard",
            "GameInitNewGameGuard",
            "GameLoadGameGuard",
            "GameFinalizeInitGuard",
            "ImpliedDefsPreResolveGuard",
            "ThingOwnerDoTickGuard",
            "SaveGameGuard",
            "LoadGameDataGuard",
            "ScribeEnterNodeGuard",
            "ScribeExitNodeGuard",
            "TickManagerTogglePausedGuard",
            "WorldObjectExposeDataGuard",
            "ThingCompPostSpawnSetupGuard",
            "WorldObjectCompInitializeGuard",
            "WorldObjectCompPostExposeDataGuard",
            "ThingCompInitializeGuard",
            "ThingCompPostExposeDataGuard",
            "ThingCompPostMapInitGuard",
            "ThingCompPostDeSpawnGuard",
            "ThingCompPostDestroyGuard",
            "WorldObjectCompPostMyMapRemovedGuard",
            "WorldObjectCompPostMapGenerateGuard",
            "WorldObjectCompPostPostRemoveGuard",
            "WorldObjectCompPostCaravanFormedGuard",
            "WorldObjectPostMakeGuard",
            "WorldObjectPostAddGuard",
            "WorldObjectPostRemoveGuard",
            "WorldObjectDestroyGuard",
            "DateNotifierTickGuard",
            "FilthMonitorTickGuard",
            "ScenarioTickGuard",
            "StoryWatcherTickGuard",
            "TransportShipManagerShipObjectsTickGuard",
            "FactionManagerTickGuard",
            "GameEnderTickGuard",
            "StorytellerTickGuard",
            "TaleManagerTickGuard",
            "QuestManagerTickGuard",
            "HistoryTickGuard",
            "AutosaverTickGuard",
            "PawnRenderTreeTrySetupGraphIfNeededGuard",
            "GraphicDatabaseGetGuard",
            "GraphicMultiInitGuard",
            "LoadTextureGuard",
            "UnityLogHardErrors",
            "UnityLogWarnings",
            "TextureWarningEscalation",
            "UnhandledException",
            "ReflectionOnlyAssemblyResolve"
        };

        internal static void Initialize(CrashCatcherSettings currentSettings)
        {
            settings = currentSettings;
            settings?.NormalizeFilters();
        }

        internal static bool ShouldEnableByDefault(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (string.Equals(key, "SaveGameGuard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "LoadGameDataGuard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "ImpliedDefsPreResolveGuard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "UnityLogHardErrors", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "UnhandledException", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        internal static IEnumerable<string> GetKnownKeys()
        {
            var keys = new HashSet<string>(explicitKeys, StringComparer.OrdinalIgnoreCase);

            if (settings?.HookFilters != null)
            {
                foreach (var filter in settings.HookFilters)
                {
                    if (!string.IsNullOrWhiteSpace(filter?.Key))
                    {
                        keys.Add(filter.Key);
                    }
                }
            }

            return keys;
        }

        internal static bool ShouldLatch(string key, Exception exception)
        {
            if (settings == null)
            {
                return true;
            }

            var filter = settings.HookFilters?.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (filter != null && !filter.Enabled)
            {
                AuditSuppressed(key, "disabled", exception);
                return false;
            }

            if (MatchesIgnorePatterns(exception))
            {
                AuditSuppressed(key, "ignored", exception);
                return false;
            }

            return true;
        }

        internal static bool IsEnabled(string key)
        {
            if (settings == null)
            {
                return true;
            }

            var filter = settings.HookFilters?.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            return filter == null || filter.Enabled;
        }

        internal static string ResolveKeyFromStack()
        {
            try
            {
                var trace = new StackTrace(2, false);
                var frame = trace.GetFrames()?.FirstOrDefault();
                var method = frame?.GetMethod();
                var typeName = method?.DeclaringType?.Name;
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    return typeName;
                }
            }
            catch
            {
            }

            return "UnknownDetector";
        }

        private static bool MatchesIgnorePatterns(Exception exception)
        {
            if (settings == null)
            {
                return false;
            }

            return MatchesTypeIgnore(exception) || MatchesMessageIgnore(exception) || MatchesCallIgnore(exception);
        }

        private static bool MatchesTypeIgnore(Exception exception)
        {
            if (exception == null || string.IsNullOrWhiteSpace(settings.ExceptionTypeIgnoreBuffer))
            {
                return false;
            }

            var typeName = exception.GetType().FullName ?? string.Empty;
            return MatchesAnyLine(settings.ExceptionTypeIgnoreBuffer, typeName);
        }

        private static bool MatchesMessageIgnore(Exception exception)
        {
            if (exception == null || string.IsNullOrWhiteSpace(settings.ExceptionMessageIgnoreBuffer))
            {
                return false;
            }

            var message = exception.Message ?? string.Empty;
            return MatchesAnyLine(settings.ExceptionMessageIgnoreBuffer, message);
        }

        private static bool MatchesCallIgnore(Exception exception)
        {
            if (exception == null || string.IsNullOrWhiteSpace(settings.CallSubstringIgnoreBuffer))
            {
                return false;
            }

            var haystack = exception.ToString() ?? string.Empty;
            return MatchesAnyLine(settings.CallSubstringIgnoreBuffer, haystack);
        }

        private static bool MatchesAnyLine(string source, string haystack)
        {
            foreach (var rawLine in source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                if (haystack.IndexOf(line, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AuditSuppressed(string key, string reason, Exception exception)
        {
            try
            {
                CallTrail.Record("filter", key, reason);
                Directory.CreateDirectory(auditFolder);
                var stackTrace = new StackTrace(2, true).ToString().Replace(Environment.NewLine, " | ");
                lock (auditGate)
                {
                    File.AppendAllText(auditPath,
                        $"{DateTime.UtcNow:O} | {key} | {reason} | {exception?.GetType().FullName ?? "null"} | {exception?.Message ?? "no message"} | {stackTrace}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }
    }
}

