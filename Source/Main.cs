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
    public static class CrashCatcherConfig
    {
        public const int MinRollingCallCount = 10;
        public const int MaxRollingCallCount = 1000;
        public const int DefaultRollingCallCount = 100;

        public static int RollingCallCount = DefaultRollingCallCount;
    }

    public sealed class CrashCatcherMod : Mod
    {
        private CrashCatcherSettings settings;
        private static readonly Dictionary<string, string> FriendlyFilterLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FirstTickGuard", "First world tick" },
            { "TickManagerUpdateGuard", "Tick manager update" },
            { "GameUpdateEntryGuard", "Game update entry" },
            { "GameUpdatePlayGuard", "Game play update" },
            { "MapUpdateGuard", "Map update" },
            { "MapPreTickGuard", "Map pre-tick" },
            { "MapPostTickGuard", "Map post-tick" },
            { "GameComponentFinalizeInitGuard", "Game component finalize init" },
            { "GameComponentStartedNewGameGuard", "Game component new game" },
            { "GameComponentLoadedGameGuard", "Game component loaded game" },
            { "WorldComponentTickGuard", "World component tick" },
            { "WorldComponentUpdateGuard", "World component update" },
            { "WorldComponentFinalizeInitGuard", "World component finalize init" },
            { "GameInitNewGameGuard", "Game init new game" },
            { "GameLoadGameGuard", "Game load game" },
            { "GameFinalizeInitGuard", "Game finalize init" },
            { "ThingOwnerDoTickGuard", "Held contents tick" },
            { "SaveGameGuard", "Save gate" },
            { "LoadGameDataGuard", "Load game data" },
            { "ScribeEnterNodeGuard", "Scribe enter node" },
            { "ScribeExitNodeGuard", "Scribe exit node" },
            { "TickManagerTogglePausedGuard", "Block unpause after crash" },
            { "WorldObjectExposeDataGuard", "World object expose data" },
            { "ThingCompPostSpawnSetupGuard", "Thing comp post-spawn setup" },
            { "WorldObjectCompInitializeGuard", "World object comp initialize" },
            { "WorldObjectCompPostExposeDataGuard", "World object comp post-expose data" },
            { "ThingCompInitializeGuard", "Thing comp initialize" },
            { "ThingCompPostExposeDataGuard", "Thing comp post-expose data" },
            { "ThingCompPostMapInitGuard", "Thing comp post-map init" },
            { "ThingCompPostDeSpawnGuard", "Thing comp post-despawn" },
            { "ThingCompPostDestroyGuard", "Thing comp post-destroy" },
            { "WorldObjectCompPostMyMapRemovedGuard", "World object comp map removed" },
            { "WorldObjectCompPostMapGenerateGuard", "World object comp map generate" },
            { "WorldObjectCompPostPostRemoveGuard", "World object comp post-remove" },
            { "WorldObjectCompPostCaravanFormedGuard", "World object comp caravan formed" },
            { "WorldObjectPostMakeGuard", "World object post-make" },
            { "WorldObjectPostAddGuard", "World object post-add" },
            { "WorldObjectPostRemoveGuard", "World object post-remove" },
            { "WorldObjectDestroyGuard", "World object destroy" },
            { "DateNotifierTickGuard", "Date notifier tick" },
            { "FilthMonitorTickGuard", "Filth monitor tick" },
            { "ScenarioTickGuard", "Scenario tick" },
            { "StoryWatcherTickGuard", "Story watcher tick" },
            { "TransportShipManagerShipObjectsTickGuard", "Transport ship manager tick" },
            { "FactionManagerTickGuard", "Faction manager tick" },
            { "GameEnderTickGuard", "Game ender tick" },
            { "StorytellerTickGuard", "Storyteller tick" },
            { "TaleManagerTickGuard", "Tale manager tick" },
            { "QuestManagerTickGuard", "Quest manager tick" },
            { "HistoryTickGuard", "History tick" },
            { "AutosaverTickGuard", "Autosaver tick" },
            { "PawnRenderTreeTrySetupGraphIfNeededGuard", "Pawn render tree setup" },
            { "GraphicDatabaseGetGuard", "Graphic database lookup" },
            { "GraphicMultiInitGuard", "Graphic multi init" },
            { "LoadTextureGuard", "Texture load" },
            { "UnityLogHardErrors", "Unity log hard errors" },
            { "UnityLogWarnings", "Unity log warnings" },
            { "TextureWarningEscalation", "Texture warning escalation" },
            { "UnhandledException", "Unhandled exception hook" },
            { "ReflectionOnlyAssemblyResolve", "Reflection-only assembly resolve" }
        };

        public CrashCatcherMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<CrashCatcherSettings>();
            CrashCatcherFilters.Initialize(settings);
            CallTrail.SetCapacity(settings.RollingCallCount);
        }

        public override string SettingsCategory()
        {
            return "CrashCatcher";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            var changed = false;

            var previousAutoArmEnabled = settings.AutoArmEnabled;
            listing.CheckboxLabeled("Auto-arm CrashCatcher (requires restart)", ref settings.AutoArmEnabled);
            changed |= settings.AutoArmEnabled != previousAutoArmEnabled;
            listing.GapLine();

            listing.Label("CrashCatcher recording settings");
            listing.GapLine();
            var previousRollingCallCount = settings.RollingCallCount;
            listing.TextFieldNumericLabeled("Rolling call count", ref settings.RollingCallCount, ref settings.rollingCallBuffer, CrashCatcherConfig.MinRollingCallCount, CrashCatcherConfig.MaxRollingCallCount);
            changed |= settings.RollingCallCount != previousRollingCallCount;
            listing.GapLine();
            listing.Label("CrashCatcher hook filters");
            listing.Label("Enable or disable individual detections:");
            foreach (var filter in settings.HookFilters.OrderBy(filter => filter.Key))
            {
                var enabled = filter.Enabled;
                listing.CheckboxLabeled(GetFriendlyFilterLabel(filter.Key), ref enabled);
                changed |= filter.Enabled != enabled;
                filter.Enabled = enabled;
            }
            listing.GapLine();
            var previousTypeIgnoreBuffer = settings.ExceptionTypeIgnoreBuffer;
            var previousMessageIgnoreBuffer = settings.ExceptionMessageIgnoreBuffer;
            var previousCallSubstringIgnoreBuffer = settings.CallSubstringIgnoreBuffer;
            settings.ExceptionTypeIgnoreBuffer = listing.TextEntryLabeled("Ignored exception types (one per line)", settings.ExceptionTypeIgnoreBuffer, 4);
            settings.ExceptionMessageIgnoreBuffer = listing.TextEntryLabeled("Ignored exception messages (one per line)", settings.ExceptionMessageIgnoreBuffer, 4);
            settings.CallSubstringIgnoreBuffer = listing.TextEntryLabeled("Ignored call substrings (one per line)", settings.CallSubstringIgnoreBuffer, 4);
            changed |= !string.Equals(previousTypeIgnoreBuffer, settings.ExceptionTypeIgnoreBuffer, StringComparison.Ordinal);
            changed |= !string.Equals(previousMessageIgnoreBuffer, settings.ExceptionMessageIgnoreBuffer, StringComparison.Ordinal);
            changed |= !string.Equals(previousCallSubstringIgnoreBuffer, settings.CallSubstringIgnoreBuffer, StringComparison.Ordinal);
            if (listing.ButtonText("Restore defaults"))
            {
                settings.ResetToDefaults();
                CallTrail.SetCapacity(settings.RollingCallCount);
                CrashCatcherFilters.Initialize(settings);
                settings.Write();
            }
            listing.End();
            settings.RollingCallCount = Mathf.Clamp(settings.RollingCallCount, CrashCatcherConfig.MinRollingCallCount, CrashCatcherConfig.MaxRollingCallCount);
            CallTrail.SetCapacity(settings.RollingCallCount);
            settings.NormalizeFilters();
            CrashCatcherFilters.Initialize(settings);
            if (changed)
            {
                settings.Write();
            }
        }

        private static string GetFriendlyFilterLabel(string key)
        {
            if (FriendlyFilterLabels.TryGetValue(key, out var label))
            {
                return label;
            }

            return key;
        }
    }

    public sealed class CrashCatcherSettings : ModSettings
    {
        public bool AutoArmEnabled = true;
        public int RollingCallCount = CrashCatcherConfig.DefaultRollingCallCount;
        public string rollingCallBuffer = CrashCatcherConfig.DefaultRollingCallCount.ToString();
        public List<HookFilterSetting> HookFilters = new List<HookFilterSetting>();
        public string ExceptionTypeIgnoreBuffer = string.Empty;
        public string ExceptionMessageIgnoreBuffer = string.Empty;
        public string CallSubstringIgnoreBuffer = string.Empty;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AutoArmEnabled, "AutoArmEnabled", true);
            Scribe_Values.Look(ref RollingCallCount, "RollingCallCount", CrashCatcherConfig.DefaultRollingCallCount);
            Scribe_Values.Look(ref ExceptionTypeIgnoreBuffer, "ExceptionTypeIgnoreBuffer", string.Empty);
            Scribe_Values.Look(ref ExceptionMessageIgnoreBuffer, "ExceptionMessageIgnoreBuffer", string.Empty);
            Scribe_Values.Look(ref CallSubstringIgnoreBuffer, "CallSubstringIgnoreBuffer", string.Empty);
            Scribe_Collections.Look(ref HookFilters, "HookFilters", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RollingCallCount = Mathf.Clamp(RollingCallCount, CrashCatcherConfig.MinRollingCallCount, CrashCatcherConfig.MaxRollingCallCount);
                rollingCallBuffer = RollingCallCount.ToString();
                CallTrail.SetCapacity(RollingCallCount);
                NormalizeFilters();
            }
        }

        internal void NormalizeFilters()
        {
            if (HookFilters == null)
            {
                HookFilters = new List<HookFilterSetting>();
            }
            foreach (var key in CrashCatcherFilters.GetKnownKeys())
            {
                if (!HookFilters.Any(filter => string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    HookFilters.Add(new HookFilterSetting
                    {
                        Key = key,
                        Enabled = CrashCatcherFilters.ShouldEnableByDefault(key)
                    });
                }
            }

            HookFilters = HookFilters
                .GroupBy(filter => filter.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(filter => filter.Key)
                .ToList();
        }

        internal void ResetToDefaults()
        {
            AutoArmEnabled = true;
            RollingCallCount = CrashCatcherConfig.DefaultRollingCallCount;
            rollingCallBuffer = RollingCallCount.ToString();
            ExceptionTypeIgnoreBuffer = string.Empty;
            ExceptionMessageIgnoreBuffer = string.Empty;
            CallSubstringIgnoreBuffer = string.Empty;

            HookFilters = new List<HookFilterSetting>();
            NormalizeFilters();
        }
    }

    public sealed class HookFilterSetting : IExposable
    {
        public string Key;
        public bool Enabled = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "Key");
            Scribe_Values.Look(ref Enabled, "Enabled", true);
        }
    }

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

    internal static class LoadPhaseTracker
    {
        private static readonly object gate = new object();
        private static string currentPhase;

        internal static void Announce(string phase, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(phase))
            {
                return;
            }

            try
            {
                lock (gate)
                {
                    if (string.Equals(currentPhase, phase, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    currentPhase = phase;
                }

                var text = string.IsNullOrWhiteSpace(detail)
                    ? $"[CrashCatcher] Load phase: {phase}"
                    : $"[CrashCatcher] Load phase: {phase} :: {detail}";

                Log.Message(text);
                CallTrail.Record("load", "CrashCatcher.LoadPhase", string.IsNullOrWhiteSpace(detail) ? phase : $"{phase} :: {detail}");
                TelemetryRecorder.RecordPhase(phase, detail);
            }
            catch
            {
            }
        }
    }

    internal static class InputTelemetry
    {
        internal static void Record(string action, string context, string detail = null)
        {
            TelemetryRecorder.RecordInput(action, context, detail);
        }
    }

    [HarmonyPatch(typeof(Verse.UIRoot_Entry), nameof(Verse.UIRoot_Entry.Init))]
    public static class UIRootEntryInitGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.ResetSession();
            TelemetryRecorder.RecordPhase("Main menu entered");
        }
    }

    [HarmonyPatch(typeof(RimWorld.MainMenuDrawer), nameof(RimWorld.MainMenuDrawer.DoMainMenuControls))]
    public static class MainMenuDrawerDoMainMenuControlsGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordInput("Opened", "MainMenu", "MainMenuControls");
        }
    }

    [HarmonyPatch(typeof(Verse.WindowStack), nameof(Verse.WindowStack.Add))]
    public static class WindowStackAddTelemetryGuard
    {
        public static void Prefix(Verse.Window window)
        {
            if (window == null)
            {
                return;
            }

            var name = window.GetType().Name;
            if (name.StartsWith("Dialog_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Page_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Screen_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("MainTabWindow_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("FloatMenu", StringComparison.OrdinalIgnoreCase))
            {
                TelemetryRecorder.RecordInput("Opened", "WindowStack", name);
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.MainTabsRoot), nameof(RimWorld.MainTabsRoot.ToggleTab))]
    public static class MainTabsRootToggleTabTelemetryGuard
    {
        public static void Prefix(RimWorld.MainButtonDef newTab)
        {
            if (newTab != null)
            {
                TelemetryRecorder.RecordInput("Clicked", "MainTab", newTab.defName);
            }
            else
            {
                TelemetryRecorder.RecordInput("Clicked", "MainTab", "Close");
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.Page_ConfigureStartingPawns), nameof(RimWorld.Page_ConfigureStartingPawns.PostOpen))]
    public static class PageConfigureStartingPawnsPostOpenTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Placing pawns");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Page_ConfigureStartingPawns), "DoNext")]
    public static class PageConfigureStartingPawnsDoNextTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordInput("Clicked", "Scenario", "Start");
            TelemetryRecorder.RecordPhase("Leaving pawn setup");
            CallTrail.Record("scenario", "Page_ConfigureStartingPawns.DoNext", "scenario intro accepted");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase("Left pawn setup");
            CallTrail.Record("scenario", "Page_ConfigureStartingPawns.DoNext", "scenario intro transition queued");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Page_SelectScenario), nameof(RimWorld.Page_SelectScenario.BeginScenarioConfiguration))]
    public static class PageSelectScenarioBeginScenarioConfigurationTelemetryGuard
    {
        public static void Prefix(RimWorld.Scenario scen)
        {
            TelemetryRecorder.RecordPhase("Scenario selected", scen?.fileName ?? scen?.name);
            CallTrail.Record("scenario", "Page_SelectScenario.BeginScenarioConfiguration", scen?.fileName ?? scen?.name);
        }

        public static void Postfix(RimWorld.Scenario scen)
        {
            TelemetryRecorder.RecordPhase("Scenario configuration started", scen?.fileName ?? scen?.name);
        }
    }

    [HarmonyPatch(typeof(RimWorld.Page_SelectStartingSite), "DoNext")]
    public static class PageSelectStartingSiteDoNextTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Starting site accepted");
            CallTrail.Record("scenario", "Page_SelectStartingSite.DoNext", "starting site confirmed");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase("Starting site transition queued");
        }
    }

    [HarmonyPatch(typeof(RimWorld.PageUtility), nameof(RimWorld.PageUtility.InitGameStart))]
    public static class PageUtilityInitGameStartTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Entering generating map long event");
            CallTrail.Record("scenario", "PageUtility.InitGameStart", "generating map long event queued");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase("Generating map long event queued");
        }
    }

    [HarmonyPatch(typeof(Verse.GameInitData), nameof(Verse.GameInitData.PrepForMapGen))]
    public static class GameInitDataPrepForMapGenTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Preparing map generation");
            CallTrail.Record("scenario", "GameInitData.PrepForMapGen", "pre-map generation started");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase("Prepared map generation");
            CallTrail.Record("scenario", "GameInitData.PrepForMapGen", "pre-map generation complete");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Scenario), nameof(RimWorld.Scenario.PreMapGenerate))]
    public static class ScenarioPreMapGenerateTelemetryGuard
    {
        public static void Prefix(RimWorld.Scenario __instance)
        {
            TelemetryRecorder.RecordPhase("Scenario pre-map generate", __instance?.fileName ?? __instance?.name);
            CallTrail.Record("scenario", "Scenario.PreMapGenerate", __instance?.fileName ?? __instance?.name);
        }

        public static void Postfix(RimWorld.Scenario __instance)
        {
            TelemetryRecorder.RecordPhase("Scenario pre-map generate complete", __instance?.fileName ?? __instance?.name);
            CallTrail.Record("scenario", "Scenario.PreMapGenerate", $"completed:{__instance?.fileName ?? __instance?.name}");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldGenerator), nameof(RimWorld.Planet.WorldGenerator.GenerateWorld))]
    public static class WorldGeneratorGenerateWorldTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Generating world");
        }

        public static void Postfix(RimWorld.Planet.World __result)
        {
            if (__result != null)
            {
                TelemetryRecorder.RecordPhase("World generated");
            }
        }
    }

    [HarmonyPatch(typeof(Verse.PlayDataLoader), nameof(Verse.PlayDataLoader.LoadAllPlayData))]
    public static class PlayDataLoaderLoadAllPlayDataTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Loading play data");
        }
    }

    [HarmonyPatch(typeof(Verse.MapGenerator), nameof(Verse.MapGenerator.GenerateMap))]
    public static class MapGeneratorGenerateMapTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Generating map");
        }

        public static void Postfix(Verse.Map __result)
        {
            if (__result != null)
            {
                TelemetryRecorder.RecordPhase("Map generated");
            }
        }
    }

    [HarmonyPatch(typeof(Verse.MapGenerator), nameof(Verse.MapGenerator.GenerateContentsIntoMap))]
    public static class MapGeneratorGenerateContentsIntoMapTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Generating terrain");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase("Terrain generated");
        }
    }

    internal static class TelemetryRecorder
    {
        private const int TrailLimit = 120;
        private static readonly object gate = new object();
        private static readonly List<string> inputEvents = new List<string>();
        private static readonly List<string> loadPhases = new List<string>();
        private static DateTimeOffset sessionStart = DateTimeOffset.UtcNow;

        internal static void ResetSession()
        {
            lock (gate)
            {
                sessionStart = DateTimeOffset.UtcNow;
                inputEvents.Clear();
                loadPhases.Clear();
            }
        }

        internal static void RecordInput(string action, string context, string detail = null)
        {
            lock (gate)
            {
                inputEvents.Add(FormatEntry("input", context, action, detail));
                Trim(inputEvents);
            }
        }

        internal static void RecordPhase(string phase, string detail = null)
        {
            lock (gate)
            {
                loadPhases.Add(FormatEntry("phase", "load", phase, detail));
                Trim(loadPhases);
            }
        }

        internal static IReadOnlyList<string> SnapshotInputs()
        {
            lock (gate)
            {
                return inputEvents.ToList();
            }
        }

        internal static IReadOnlyList<string> SnapshotPhases()
        {
            lock (gate)
            {
                return loadPhases.ToList();
            }
        }

        private static string FormatEntry(string kind, string context, string action, string detail)
        {
            var elapsed = DateTimeOffset.UtcNow - sessionStart;
            return string.IsNullOrWhiteSpace(detail)
                ? $"[{elapsed:mm\\:ss\\.fff}] {kind}:{context}:{action}"
                : $"[{elapsed:mm\\:ss\\.fff}] {kind}:{context}:{action} :: {detail}";
        }

        private static void Trim(List<string> entries)
        {
            while (entries.Count > TrailLimit)
            {
                entries.RemoveAt(0);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            try
            {
                var mod = LoadedModManager.GetMod<CrashCatcherMod>();
                var settings = mod?.GetSettings<CrashCatcherSettings>();
                if (settings != null && !settings.AutoArmEnabled)
                {
                    Log.Message("[CrashCatcher] Auto-arm disabled; running passive (no hooks installed).");
                    return;
                }

                CrashCatcherHooks.Install();
                var harmony = new Harmony("JellyCreative.CrashCatcher");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[CrashCatcher] Armed.");
            }
            catch (Exception ex)
            {
                Log.Error($"[CrashCatcher] Failed to initialize:\n{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class FirstTickGuard
    {
        private static bool armed = true;
        internal static bool crashLatched;
        internal static readonly Color FrozenTint = new Color(0.25f, 0.25f, 0.25f, 0.85f);

        public static void Prefix()
        {
            CallTrail.Record("tick", "TickManager.DoSingleTick");
            if (crashLatched)
            {
                if (Find.TickManager != null)
                {
                    Find.TickManager.Pause();
                }
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (!armed) return __exception;

            armed = false;
            if (__exception == null) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class ImpliedDefsPreResolveGuard
    {
        private static bool armed = true;

        public static void Prefix()
        {
            LoadPhaseTracker.Announce("Resolving implied defs");
            CallTrail.Record("load", "DefGenerator.GenerateImpliedDefs_PreResolve");
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (!armed) return __exception;

            armed = false;
            if (__exception == null) return null;

            CrashCrashHandler.Latch(__exception, "ImpliedDefsPreResolveGuard");
            return null;
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    public static class TickManagerUpdateGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("tick", "TickManager.TickManagerUpdate");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.UpdateEntry))]
    public static class UpdateEntryGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("tick", "Game.UpdateEntry");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.UpdatePlay))]
    public static class PlayUpdateGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("tick", "Game.UpdatePlay");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                PauseNotice.TryShow();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapUpdate))]
    public static class MapUpdateGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("map", "Map.MapUpdate");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapPreTick))]
    public static class MapPreTickGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("map", "Map.MapPreTick");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapPostTick))]
    public static class MapPostTickGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("map", "Map.MapPostTick");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.MapComponentTick))]
    public static class MapComponentTickGuard
    {
        public static void Prefix(Map map)
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, Map map)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.MapComponentUpdate))]
    public static class MapComponentUpdateGuard
    {
        public static void Prefix(Map map)
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, Map map)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.FinalizeInit))]
    public static class MapComponentFinalizeInitGuard
    {
        public static void Prefix(Map map)
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, Map map)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.MapGenerated))]
    public static class MapComponentMapGeneratedGuard
    {
        public static void Prefix(Map map)
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, Map map)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.MapRemoved))]
    public static class MapComponentMapRemovedGuard
    {
        public static void Prefix(Map map)
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, Map map)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit))]
    public static class GameComponentFinalizeInitGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "GameComponentUtility.FinalizeInit");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.StartedNewGame))]
    public static class GameComponentStartedNewGameGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "GameComponentUtility.StartedNewGame");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.LoadedGame))]
    public static class GameComponentLoadedGameGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "GameComponentUtility.LoadedGame");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldComponentUtility), nameof(RimWorld.Planet.WorldComponentUtility.WorldComponentTick))]
    public static class WorldComponentTickGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldComponentUtility.WorldComponentTick");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldComponentUtility), nameof(RimWorld.Planet.WorldComponentUtility.WorldComponentUpdate))]
    public static class WorldComponentUpdateGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldComponentUtility.WorldComponentUpdate");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldComponentUtility), nameof(RimWorld.Planet.WorldComponentUtility.FinalizeInit))]
    public static class WorldComponentFinalizeInitGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldComponentUtility.FinalizeInit");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, RimWorld.Planet.World world, bool fromLoad)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class InitNewGameGuard
    {
        public static void Prefix()
        {
            LoadPhaseTracker.Announce("Starting new game");
            CallTrail.Record("load", "Game.InitNewGame");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class LoadGameGuard
    {
        public static void Prefix()
        {
            LoadPhaseTracker.Announce("Loading game");
            CallTrail.Record("load", "Game.LoadGame");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class GameFinalizeInitGuard
    {
        public static void Prefix()
        {
            LoadPhaseTracker.Announce("Finalizing game init");
            CallTrail.Record("load", "Game.FinalizeInit");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(TickList), nameof(TickList.Tick))]
    public static class TickListTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
    public static class ThingDoTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
    public static class ThingTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "TickInterval")]
    public static class ThingTickIntervalGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "TickRare")]
    public static class ThingTickRareGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "TickLong")]
    public static class ThingTickLongGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingOwner), nameof(ThingOwner.DoTick))]
    public static class ThingOwnerDoTickGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("tick", "ThingOwner.DoTick");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.CompTick))]
    public static class ThingCompTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.CompTickInterval))]
    public static class ThingCompTickIntervalGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.CompTickRare))]
    public static class ThingCompTickRareGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.CompTickLong))]
    public static class ThingCompTickLongGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(PawnRenderTree), "TrySetupGraphIfNeeded")]
    public static class PawnRenderTreeGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.WorldTick))]
    public static class WorldTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.WorldPostTick))]
    public static class WorldPostTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.WorldUpdate))]
    public static class WorldUpdateGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(GraphicDatabase), nameof(GraphicDatabase.Get), new Type[] { typeof(Type), typeof(string), typeof(Shader), typeof(Vector2), typeof(Color), typeof(Color), typeof(GraphicData), typeof(System.Collections.Generic.List<ShaderParameter>), typeof(string) })]
    public static class GraphicDatabaseGetGuard
    {
        public static void Prefix(Type graphicClass)
        {
            if (graphicClass == typeof(Graphic_Multi))
            {
                CrashCatcherHooks.TryLatchPendingTextureWarning();
            }
        }

        public static Exception Finalizer(Exception __exception, Type graphicClass)
        {
            if (__exception != null && !FirstTickGuard.crashLatched)
            {
                CrashCrashHandler.Latch(__exception);
                return null;
            }

            if (graphicClass == typeof(Graphic_Multi))
            {
                CrashCatcherHooks.TryLatchPendingTextureWarning();
                if (FirstTickGuard.crashLatched)
                {
                    CrashBackdrop.EnsureVisible();
                }
            }

            return __exception;
        }
    }

    [HarmonyPatch(typeof(Graphic_Multi), nameof(Graphic_Multi.Init))]
    public static class GraphicMultiInitGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsUpdate))]
    public static class LongEventsUpdateGuard
    {
        public static void Prefix()
        {
            LoadPhaseTracker.Announce("Updating long events");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception, ref bool sceneChanged)
        {
            if (__exception == null)
            {
                return null;
            }

            if (!FirstTickGuard.crashLatched)
            {
                CrashCrashHandler.Latch(__exception);
            }

            sceneChanged = true;
            return null;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentTick))]
    public static class GameComponentTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentUpdate))]
    public static class GameComponentUpdateGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(FactionManager), nameof(FactionManager.FactionManagerTick))]
    public static class FactionManagerTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(GameEnder), nameof(GameEnder.GameEndTick))]
    public static class GameEnderTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.StorytellerTick))]
    public static class StorytellerTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(TaleManager), nameof(TaleManager.TaleManagerTick))]
    public static class TaleManagerTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.QuestManagerTick))]
    public static class QuestManagerTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(History), nameof(History.HistoryTick))]
    public static class HistoryTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Autosaver), nameof(Autosaver.AutosaverTick))]
    public static class AutosaverTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(DateNotifier), nameof(DateNotifier.DateNotifierTick))]
    public static class DateNotifierTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch]
    public static class FilthMonitorTickGuard
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("RimWorld.FilthMonitor:FilthMonitorTick");
        }

        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(Scenario), nameof(Scenario.TickScenario))]
    public static class ScenarioTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), "Tick")]
    public static class WorldObjectTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), "TickInterval")]
    public static class WorldObjectTickIntervalGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.CompTick))]
    public static class WorldObjectCompTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.CompTickInterval))]
    public static class WorldObjectCompTickIntervalGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), nameof(RimWorld.Planet.WorldObject.ExposeData))]
    public static class WorldObjectExposeDataGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("save", "WorldObject.ExposeData");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostSpawnSetup))]
    public static class ThingCompPostSpawnSetupGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "ThingComp.PostSpawnSetup");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.Initialize))]
    public static class WorldObjectCompInitializeGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "WorldObjectComp.Initialize");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.PostExposeData))]
    public static class WorldObjectCompPostExposeDataGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("save", "WorldObjectComp.PostExposeData");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.Initialize))]
    public static class ThingCompInitializeGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "ThingComp.Initialize");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostExposeData))]
    public static class ThingCompPostExposeDataGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("save", "ThingComp.PostExposeData");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostMapInit))]
    public static class ThingCompPostMapInitGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "ThingComp.PostMapInit");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostPostMake))]
    public static class ThingCompPostPostMakeGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostDeSpawn))]
    public static class ThingCompPostDeSpawnGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "ThingComp.PostDeSpawn");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.PostDestroy))]
    public static class ThingCompPostDestroyGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "ThingComp.PostDestroy");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.PostDestroy))]
    public static class WorldObjectCompPostDestroyGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.PostMyMapRemoved))]
    public static class WorldObjectCompPostMyMapRemovedGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObjectComp.PostMyMapRemoved");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.PostMapGenerate))]
    public static class WorldObjectCompPostMapGenerateGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObjectComp.PostMapGenerate");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.PostPostRemove))]
    public static class WorldObjectCompPostPostRemoveGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObjectComp.PostPostRemove");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObjectComp), nameof(RimWorld.Planet.WorldObjectComp.PostCaravanFormed))]
    public static class WorldObjectCompPostCaravanFormedGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObjectComp.PostCaravanFormed");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), nameof(RimWorld.Planet.WorldObject.PostMake))]
    public static class WorldObjectPostMakeGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("component", "WorldObject.PostMake");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), nameof(RimWorld.Planet.WorldObject.PostAdd))]
    public static class WorldObjectPostAddGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObject.PostAdd");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), nameof(RimWorld.Planet.WorldObject.PostRemove))]
    public static class WorldObjectPostRemoveGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObject.PostRemove");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldObject), nameof(RimWorld.Planet.WorldObject.Destroy))]
    public static class WorldObjectDestroyGuard
    {
        public static void Prefix()
        {
            CallTrail.Record("world", "WorldObject.Destroy");
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(StoryWatcher), nameof(StoryWatcher.StoryWatcherTick))]
    public static class StoryWatcherTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(TransportShipManager), nameof(TransportShipManager.ShipObjectsTick))]
    public static class TransportShipManagerTickGuard
    {
        public static void Prefix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(ModContentLoader<Texture2D>), "LoadTexture", MethodType.Normal)]
    public static class TextureLoadGuard
    {
        public static void Postfix()
        {
            CrashCatcherHooks.TryLatchPendingTextureWarning();
            if (FirstTickGuard.crashLatched)
            {
                CrashBackdrop.EnsureVisible();
            }
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    public static class RootUpdateGuard
    {
        public static void Prefix()
        {
            if (FirstTickGuard.crashLatched)
            {
                PauseNotice.TryShow();
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.CreateModClasses))]
    public static class CreateModClassesGuard
    {
        public static void Prefix()
        {
            LoadPhaseTracker.Announce("Creating mod classes");
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (FirstTickGuard.crashLatched) return null;

            CrashCrashHandler.Latch(__exception);
            return null;
        }
    }

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
                Log.Warning($"[CrashCatcher] Dry-run cleanup failed: {cleanupEx.Message}");
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
            builder.AppendLine();
            builder.AppendLine("--- Last 100 calls ---");
            builder.AppendLine(CallTrail.Dump());

            if (!string.IsNullOrWhiteSpace(result.DebugXml))
            {
                builder.AppendLine();
                builder.AppendLine("--- Dry-run XML ---");
                builder.AppendLine(result.DebugXml);
            }

            File.WriteAllText(LastReportPath, builder.ToString());
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
                Log.Warning(text);
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

    internal static class CrashCrashHandler
    {
        internal static void Latch(Exception exception)
        {
            Latch(exception, CrashCatcherFilters.ResolveKeyFromStack());
        }

        internal static void Latch(Exception exception, string sourceKey)
        {
            if (FirstTickGuard.crashLatched)
            {
                return;
            }
            if (!CrashCatcherFilters.ShouldLatch(sourceKey, exception))
            {
                return;
            }
            FirstTickGuard.crashLatched = true;
            CrashBackdrop.EnsureVisible();
            Log.Message($"[CrashCatcher] Catcher active ({sourceKey}).");
            CrashReportWriter.Write(exception);
            PauseNotice.Queue(exception);
            CrashCatcherHooks.OpenWatchdogGate();
        }
    }

    internal static class CallTrail
    {
        private static readonly object gate = new object();
        private static readonly Queue<TrailEntry> entries = new Queue<TrailEntry>(CrashCatcherConfig.DefaultRollingCallCount);
        private static int capacity = CrashCatcherConfig.DefaultRollingCallCount;

        internal static void SetCapacity(int value)
        {
            try
            {
                lock (gate)
                {
                    capacity = Mathf.Clamp(value, CrashCatcherConfig.MinRollingCallCount, CrashCatcherConfig.MaxRollingCallCount);
                    while (entries.Count > capacity)
                    {
                        entries.Dequeue();
                    }
                }
            }
            catch
            {
            }
        }

        internal static void Record(string phase, string call, string detail = null)
        {
            try
            {
                lock (gate)
                {
                    if (entries.Count >= capacity)
                    {
                        entries.Dequeue();
                    }

                    entries.Enqueue(new TrailEntry
                    {
                        Tick = Find.TickManager != null ? Find.TickManager.TicksGame : -1,
                        Phase = phase,
                        Call = call,
                        Detail = detail
                    });
                }
            }
            catch
            {
            }
        }

        internal static string Dump()
        {
            try
            {
                lock (gate)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var entry in entries)
                    {
                        sb.AppendLine($"{entry.Tick}\t[{entry.Phase}] {entry.Call}{(entry.Detail.NullOrEmpty() ? "" : " :: " + entry.Detail)}");
                    }

                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                return "[CrashCatcher] Failed to dump call trail:\n" + ex;
            }
        }

        private sealed class TrailEntry
        {
            public int Tick;
            public string Phase;
            public string Call;
            public string Detail;
        }
    }

    internal static class CrashCatcherHooks
    {
        private static bool installed;
        private static ManualResetEventSlim crashLatchGate;
        private static Thread watchdogThread;
        private static readonly object pendingTextureWarningGate = new object();
        private static string pendingTextureWarning;
        private static int quitRequestArmed;

        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;
            crashLatchGate = new ManualResetEventSlim(false);
            watchdogThread = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = "CrashCatcher Watchdog"
            };
            watchdogThread.Start();
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.wantsToQuit += OnWantsToQuit;
            Log.Message("[CrashCatcher] Log hook installed.");
        }

        private static void WatchdogLoop()
        {
            try
            {
                crashLatchGate.Wait();
                while (true)
                {
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            catch
            {
                // Keep the process alive even if the host is tearing down.
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                CrashCrashHandler.Latch(exception, "UnhandledException");
            }
        }

        private static Assembly OnReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var requested = new AssemblyName(args.Name);
                var managedDir = GetManagedDirectory();
                if (managedDir == null)
                {
                    return null;
                }

                var candidate = Path.Combine(managedDir, requested.Name + ".dll");
                if (File.Exists(candidate))
                {
                    return Assembly.ReflectionOnlyLoadFrom(candidate);
                }

                var loaded = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in loaded)
                {
                    try
                    {
                        var location = asm.Location;
                        if (string.IsNullOrEmpty(location))
                        {
                            continue;
                        }

                        if (string.Equals(Path.GetFileNameWithoutExtension(location), requested.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return Assembly.ReflectionOnlyLoadFrom(location);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Reflection-only resolve failed for {args.Name}: {ex.Message}");
            }

            return null;
        }

        internal static void OpenWatchdogGate()
        {
            crashLatchGate?.Set();
        }

        internal static void RequestQuit()
        {
            Interlocked.Exchange(ref quitRequestArmed, 1);
            Log.Message("[CrashCatcher] Close game requested.");
            Application.Quit();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(1500);
                    if (Interlocked.Exchange(ref quitRequestArmed, 0) == 1)
                    {
                        Log.Warning("[CrashCatcher] Close game fallback forcing process exit.");
                        Environment.Exit(0);
                    }
                }
                catch
                {
                    try
                    {
                        Environment.Exit(0);
                    }
                    catch
                    {
                    }
                }
            });
        }

        private static bool OnWantsToQuit()
        {
            if (!FirstTickGuard.crashLatched)
            {
                return true;
            }

            if (Interlocked.Exchange(ref quitRequestArmed, 0) == 1)
            {
                return true;
            }

            CrashBackdrop.EnsureVisible();
            PauseNotice.TryShow();
            Log.Message("[CrashCatcher] Quit blocked by fail-safe latch.");
            return false;
        }

        private static void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (FirstTickGuard.crashLatched)
            {
                return;
            }

            var isTextureCompressionWarning =
                condition.Contains("Compress will not work", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("dimensions are not multiples of 4", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("Texture '' has dimensions", StringComparison.OrdinalIgnoreCase);

            if (type == LogType.Warning && !isTextureCompressionWarning && !CrashCatcherFilters.IsEnabled("UnityLogWarnings"))
            {
                return;
            }

            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert && !isTextureCompressionWarning && !CrashCatcherFilters.IsEnabled("UnityLogWarnings"))
            {
                return;
            }

            if (condition.NullOrEmpty())
            {
                return;
            }

            if (condition.StartsWith("[CrashCatcher]"))
            {
                return;
            }

            if (isTextureCompressionWarning)
            {
                if (!CrashCatcherFilters.IsEnabled("TextureWarningEscalation"))
                {
                    CallTrail.Record("filter", "TextureWarningEscalation", "disabled");
                    return;
                }

                lock (pendingTextureWarningGate)
                {
                    pendingTextureWarning = condition;
                }

                TelemetryRecorder.RecordPhase("Unity log texture warning", condition);
                CallTrail.Record("log", "UnityLog", "TextureWarningEscalation");
                var textureException = new Exception($"Texture warning triggered crash catcher.\n{condition}\n{stackTrace}");
                CrashCrashHandler.Latch(textureException, "TextureWarningEscalation");
                return;
            }

            if (type == LogType.Warning)
            {
                TelemetryRecorder.RecordPhase("Unity log warning", condition);
                CallTrail.Record("log", "UnityLog", "UnityLogWarnings");
                var warningException = new Exception($"Unity warning triggered crash catcher.\n{condition}\n{stackTrace}");
                CrashCrashHandler.Latch(warningException, "UnityLogWarnings");
                return;
            }

            TelemetryRecorder.RecordPhase("Unity log hard error", condition);
            CallTrail.Record("log", "UnityLog", "UnityLogHardErrors");
            var hardException = new Exception($"Unity log triggered crash catcher.\n{condition}\n{stackTrace}");
            CrashCrashHandler.Latch(hardException, "UnityLogHardErrors");
        }

        internal static void TryLatchPendingTextureWarning()
        {
            if (FirstTickGuard.crashLatched)
            {
                return;
            }

            string warning;
            lock (pendingTextureWarningGate)
            {
                warning = pendingTextureWarning;
                pendingTextureWarning = null;
            }

            if (warning.NullOrEmpty())
            {
                return;
            }

            var exception = new Exception($"Texture warning escalated inside the game loop.\n{warning}");
            CrashCrashHandler.Latch(exception, "TextureWarningEscalation");
        }

        private static string GetManagedDirectory()
        {
            try
            {
                var dataPath = UnityData.dataPath;
                if (string.IsNullOrWhiteSpace(dataPath))
                {
                    return null;
                }

                var rootDir = Directory.GetParent(dataPath);
                if (rootDir == null)
                {
                    return null;
                }

                var managedDir = Path.Combine(rootDir.FullName, "RimWorldWin64_Data", "Managed");
                return Directory.Exists(managedDir) ? managedDir : null;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class CrashReportWriter
    {
        internal static string LastReportPath { get; private set; }

        internal static void Write(Exception exception)
        {
            try
            {
                var dir = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, "FirstTickCrash.log");
                var builder = new StringBuilder();
                builder.AppendLine(exception.ToString());
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
                builder.AppendLine();
                builder.AppendLine("--- Last 100 calls ---");
                builder.AppendLine(CallTrail.Dump());
                File.WriteAllText(LastReportPath, builder.ToString());
            }
            catch (Exception ex)
            {
                Log.Error($"[CrashCatcher] Failed to write crash report:\n{ex}");
            }
        }
    }

    internal static class PauseNotice
    {
        private static CrashNoticeWindow activeWindow;
        private static Exception pendingException;

        internal static void Queue(Exception exception)
        {
            pendingException = exception;
        }

        internal static void TryShow()
        {
            if (pendingException == null || activeWindow != null)
            {
                return;
            }

            if (Find.WindowStack == null || Find.TickManager == null)
            {
                return;
            }

            try
            {
                CrashBackdrop.EnsureVisible();
                var reportPath = CrashReportWriter.LastReportPath ?? Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher", "FirstTickCrash.log");
                var message = pendingException.Message;
                if (message.Contains("Compress will not work", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("dimensions are not multiples of 4", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Texture '' has dimensions", StringComparison.OrdinalIgnoreCase))
                {
                    message = "CrashCatcher detected a texture compression problem and froze the game before the render could continue.";
                }

                var body = $"CrashCatcher intercepted the first world tick failure and held the game paused.\n\n{message}\n\nCrash report:\n{reportPath}\n\nThis notice cannot be dismissed. Use Close game if you want to exit.";

                activeWindow = new CrashNoticeWindow(body, reportPath);
                Find.WindowStack.Add(activeWindow);
                pendingException = null;
            }
            catch (Exception ex)
            {
                Log.Error($"[CrashCatcher] Failed to show pause notice:\n{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Verse.UIRoot_Entry), nameof(Verse.UIRoot_Entry.Init))]
    public static class UIRootEntryInitTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase("Main menu entered");
        }
    }

    internal static class CrashBackdrop
    {
        private static CrashBackdropWindow activeBackdrop;

        internal static void EnsureVisible()
        {
            if (!FirstTickGuard.crashLatched)
            {
                return;
            }

            if (Find.WindowStack == null)
            {
                return;
            }

            if (activeBackdrop != null)
            {
                return;
            }

            activeBackdrop = new CrashBackdropWindow();
            Find.WindowStack.Add(activeBackdrop);
        }
    }

    internal sealed class CrashBackdropWindow : Window
    {
        private readonly float glitchSeed = (float)DateTime.UtcNow.Ticks % 1000f;

        internal CrashBackdropWindow()
        {
            forcePause = true;
            absorbInputAroundWindow = false;
            doCloseX = false;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            preventCameraMotion = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        public override void DoWindowContents(Rect inRect)
        {
            var time = Time.realtimeSinceStartup + glitchSeed;

            GUI.color = FirstTickGuard.FrozenTint;
            Widgets.DrawBoxSolid(inRect, GUI.color);

            GUI.color = new Color(0.35f, 0.5f, 0.9f, 0.08f);
            for (int i = 0; i < 4; i++)
            {
                float y = Mathf.Repeat(time * 52f + i * 43f, inRect.height);
                float height = 2f + Mathf.PingPong(time * 6f + i, 3f);
                Widgets.DrawBoxSolid(new Rect(0f, y, inRect.width, height), GUI.color);
            }

            GUI.color = new Color(1f, 0.2f, 0.35f, 0.05f);
            float bandY = Mathf.Repeat(time * 31f, inRect.height);
            Widgets.DrawBoxSolid(new Rect(0f, bandY, inRect.width, 4f), GUI.color);
            GUI.color = Color.white;
        }

        public override bool OnCloseRequest() => false;
        public override void OnCancelKeyPressed() { }
        public override void OnAcceptKeyPressed() { }
    }

    internal sealed class CrashNoticeWindow : Window
    {
        private readonly string body;
        private readonly string reportPath;
        private readonly float glitchSeed = (float)DateTime.UtcNow.Ticks % 1000f;

        internal CrashNoticeWindow(string body, string reportPath)
        {
            this.body = body;
            this.reportPath = reportPath;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = false;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            preventCameraMotion = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(720f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Text.Font = GameFont.Small;
            var wobble = Mathf.Sin((Time.realtimeSinceStartup + glitchSeed) * 8f) * 1.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            Widgets.Label(new Rect(wobble, y, inRect.width, inRect.height - 80f), body);
            GUI.color = Color.white;
            y = inRect.height - 40f;

            var halfWidth = (inRect.width - 10f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, halfWidth, 40f), "Open report folder"))
            {
                try
                {
                    var folder = Path.GetDirectoryName(reportPath);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CrashCatcher] Failed to open crash report folder:\n{ex}");
                }
            }

            if (Widgets.ButtonText(new Rect(halfWidth + 10f, y, halfWidth, 40f), "Close game"))
            {
                CrashCatcherHooks.RequestQuit();
            }
        }

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            if (Find.WindowStack == null || Find.WindowStack.currentlyDrawnWindow != this)
            {
                return;
            }
            GUI.color = new Color(1f, 0.9f, 0.3f, 0.85f);
            var labelRect = new Rect(24f, 24f, UI.screenWidth - 48f, 24f);
            Widgets.Label(labelRect, "[CrashCatcher] Catcher active");
            GUI.color = Color.white;
        }

        public override bool OnCloseRequest()
        {
            return false;
        }

        public override void OnCancelKeyPressed()
        {
        }

        public override void OnAcceptKeyPressed()
        {
        }
    }
}
