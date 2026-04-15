using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class StartupPhaseMarkers
    {
        internal static readonly string[] Ordered =
        {
            "Page_ConfigureStartingPawns.DoNext",
            "PageUtility.InitGameStart",
            "GameInitData.PrepForMapGen",
            "Scenario.PreMapGenerate",
            "WorldGenerator.GenerateWorld",
            "MapGenerator.GenerateMap",
            "MapGenerator.GenerateContentsIntoMap"
        };

        internal const string PageConfigureStartingPawnsDoNext = "Page_ConfigureStartingPawns.DoNext";
        internal const string PageUtilityInitGameStart = "PageUtility.InitGameStart";
        internal const string GameInitDataPrepForMapGen = "GameInitData.PrepForMapGen";
        internal const string ScenarioPreMapGenerate = "Scenario.PreMapGenerate";
        internal const string WorldGeneratorGenerateWorld = "WorldGenerator.GenerateWorld";
        internal const string MapGeneratorGenerateMap = "MapGenerator.GenerateMap";
        internal const string MapGeneratorGenerateContentsIntoMap = "MapGenerator.GenerateContentsIntoMap";
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
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.PageConfigureStartingPawnsDoNext, "scenario intro accepted");
            TelemetryRecorder.RecordPhase("Leaving pawn setup");
            CallTrail.Record("scenario", "Page_ConfigureStartingPawns.DoNext", "scenario intro accepted");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.PageConfigureStartingPawnsDoNext, "scenario intro transition queued");
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
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.PageUtilityInitGameStart, "begin");
            TelemetryRecorder.RecordPhase("Entering generating map long event");
            CallTrail.Record("scenario", "PageUtility.InitGameStart", "generating map long event queued");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.PageUtilityInitGameStart, "queued");
            TelemetryRecorder.RecordPhase("Generating map long event queued");
        }
    }

    [HarmonyPatch(typeof(Verse.GameInitData), nameof(Verse.GameInitData.PrepForMapGen))]
    public static class GameInitDataPrepForMapGenTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.GameInitDataPrepForMapGen, "begin");
            TelemetryRecorder.RecordPhase("Preparing map generation");
            CallTrail.Record("scenario", "GameInitData.PrepForMapGen", "pre-map generation started");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.GameInitDataPrepForMapGen, "complete");
            TelemetryRecorder.RecordPhase("Prepared map generation");
            CallTrail.Record("scenario", "GameInitData.PrepForMapGen", "pre-map generation complete");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Scenario), nameof(RimWorld.Scenario.PreMapGenerate))]
    public static class ScenarioPreMapGenerateTelemetryGuard
    {
        public static void Prefix(RimWorld.Scenario __instance)
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.ScenarioPreMapGenerate, __instance?.fileName ?? __instance?.name);
            TelemetryRecorder.RecordPhase("Scenario pre-map generate", __instance?.fileName ?? __instance?.name);
            CallTrail.Record("scenario", "Scenario.PreMapGenerate", __instance?.fileName ?? __instance?.name);
        }

        public static void Postfix(RimWorld.Scenario __instance)
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.ScenarioPreMapGenerate, $"completed:{__instance?.fileName ?? __instance?.name}");
            TelemetryRecorder.RecordPhase("Scenario pre-map generate complete", __instance?.fileName ?? __instance?.name);
            CallTrail.Record("scenario", "Scenario.PreMapGenerate", $"completed:{__instance?.fileName ?? __instance?.name}");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldGenerator), nameof(RimWorld.Planet.WorldGenerator.GenerateWorld))]
    public static class WorldGeneratorGenerateWorldTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.WorldGeneratorGenerateWorld, "begin");
            TelemetryRecorder.RecordPhase("Generating world");
        }

        public static void Postfix(RimWorld.Planet.World __result)
        {
            if (__result != null)
            {
                TelemetryRecorder.RecordPhase(StartupPhaseMarkers.WorldGeneratorGenerateWorld, "complete");
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
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.MapGeneratorGenerateMap, "begin");
            TelemetryRecorder.RecordPhase("Generating map");
        }

        public static void Postfix(Verse.Map __result)
        {
            if (__result != null)
            {
                TelemetryRecorder.RecordPhase(StartupPhaseMarkers.MapGeneratorGenerateMap, "complete");
                TelemetryRecorder.RecordPhase("Map generated");
            }
        }
    }

    [HarmonyPatch(typeof(Verse.MapGenerator), nameof(Verse.MapGenerator.GenerateContentsIntoMap))]
    public static class MapGeneratorGenerateContentsIntoMapTelemetryGuard
    {
        public static void Prefix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.MapGeneratorGenerateContentsIntoMap, "begin");
            TelemetryRecorder.RecordPhase("Generating terrain");
        }

        public static void Postfix()
        {
            TelemetryRecorder.RecordPhase(StartupPhaseMarkers.MapGeneratorGenerateContentsIntoMap, "complete");
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
}
