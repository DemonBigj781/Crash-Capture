using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int TabButtonsHeight = 35;
        private enum ShortcutSettingsTab
        {
            ButtonCombos,
            TextSentences
        }
        private static ShortcutSettingsTab selectedShortcutTab = ShortcutSettingsTab.ButtonCombos;
        private static readonly Dictionary<string, string> FriendlyFilterLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FirstTickGuard", "First world tick" },
            { "TickManagerDoSingleTickGuard", "First world tick" },
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

            listing.Label("CrashCatcher shortcuts");
            var tabRect = listing.GetRect(TabButtonsHeight);
            DrawShortcutTabs(tabRect);
            if (selectedShortcutTab == ShortcutSettingsTab.ButtonCombos)
            {
                DrawButtonCombosTab(listing, ref changed, settings);
            }
            else
            {
                DrawTextSentencesTab(listing, ref changed, settings);
            }
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

        private static void DrawShortcutTabs(Rect tabRect)
        {
            var halfWidth = tabRect.width / 2f;
            var leftRect = new Rect(tabRect.x, tabRect.y, halfWidth - 2f, tabRect.height);
            var rightRect = new Rect(tabRect.x + halfWidth + 2f, tabRect.y, halfWidth - 2f, tabRect.height);

            if (Widgets.ButtonText(leftRect, "Button Combos"))
            {
                selectedShortcutTab = ShortcutSettingsTab.ButtonCombos;
            }

            if (Widgets.ButtonText(rightRect, "Text / Sentences"))
            {
                selectedShortcutTab = ShortcutSettingsTab.TextSentences;
            }
        }

        private static void DrawButtonCombosTab(Listing_Standard listing, ref bool changed, CrashCatcherSettings settings)
        {
            var previousHoverDefinitionCopy = settings.EnableHoverDefinitionCopy;
            var previousTickSequenceRecording = settings.EnableTickSequenceRecording;
            listing.CheckboxLabeled("Enable hover definition copy (Ctrl+Shift+C)", ref settings.EnableHoverDefinitionCopy);
            listing.CheckboxLabeled("Enable tick sequence recording (Ctrl+Shift+X)", ref settings.EnableTickSequenceRecording);
            changed |= settings.EnableHoverDefinitionCopy != previousHoverDefinitionCopy;
            changed |= settings.EnableTickSequenceRecording != previousTickSequenceRecording;
        }

        private static void DrawTextSentencesTab(Listing_Standard listing, ref bool changed, CrashCatcherSettings settings)
        {
            var previousHoverDebugContext = settings.HoverCopyIncludeDebugContext;
            var previousHoverParentChain = settings.HoverCopyIncludeParentChain;
            var previousHoverCursorPosition = settings.HoverCopyIncludeCursorPosition;
            var previousTickSequenceSummaryToClipboard = settings.CopyTickSequenceSummaryToClipboard;
            var previousTickSequenceIncludePhases = settings.IncludeLiveGameLoopPhases;
            var previousTickSequenceIncludeMapTickEvents = settings.IncludeMapTickEvents;
            var previousTickSequenceIncludeThingComponentTickEvents = settings.IncludeThingComponentTickEvents;

            listing.CheckboxLabeled("Include debug context", ref settings.HoverCopyIncludeDebugContext);
            listing.CheckboxLabeled("Copy parent chain", ref settings.HoverCopyIncludeParentChain);
            listing.CheckboxLabeled("Copy cursor position", ref settings.HoverCopyIncludeCursorPosition);
            listing.CheckboxLabeled("Copy summary to clipboard", ref settings.CopyTickSequenceSummaryToClipboard);
            listing.CheckboxLabeled("Include live loop phase labels", ref settings.IncludeLiveGameLoopPhases);
            listing.CheckboxLabeled("Include map tick events", ref settings.IncludeMapTickEvents);
            listing.CheckboxLabeled("Include thing component tick events", ref settings.IncludeThingComponentTickEvents);

            changed |= settings.HoverCopyIncludeDebugContext != previousHoverDebugContext;
            changed |= settings.HoverCopyIncludeParentChain != previousHoverParentChain;
            changed |= settings.HoverCopyIncludeCursorPosition != previousHoverCursorPosition;
            changed |= settings.CopyTickSequenceSummaryToClipboard != previousTickSequenceSummaryToClipboard;
            changed |= settings.IncludeLiveGameLoopPhases != previousTickSequenceIncludePhases;
            changed |= settings.IncludeMapTickEvents != previousTickSequenceIncludeMapTickEvents;
            changed |= settings.IncludeThingComponentTickEvents != previousTickSequenceIncludeThingComponentTickEvents;
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
        public bool EnableHoverDefinitionCopy = false;
        public bool HoverCopyIncludeDebugContext = true;
        public bool HoverCopyIncludeParentChain = true;
        public bool HoverCopyIncludeCursorPosition = true;
        public bool EnableTickSequenceRecording = false;
        public bool CopyTickSequenceSummaryToClipboard = true;
        public bool IncludeLiveGameLoopPhases = true;
        public bool IncludeMapTickEvents = true;
        public bool IncludeThingComponentTickEvents = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AutoArmEnabled, "AutoArmEnabled", true);
            Scribe_Values.Look(ref RollingCallCount, "RollingCallCount", CrashCatcherConfig.DefaultRollingCallCount);
            Scribe_Values.Look(ref ExceptionTypeIgnoreBuffer, "ExceptionTypeIgnoreBuffer", string.Empty);
            Scribe_Values.Look(ref ExceptionMessageIgnoreBuffer, "ExceptionMessageIgnoreBuffer", string.Empty);
            Scribe_Values.Look(ref CallSubstringIgnoreBuffer, "CallSubstringIgnoreBuffer", string.Empty);
            Scribe_Values.Look(ref EnableHoverDefinitionCopy, "EnableHoverDefinitionCopy", false);
            Scribe_Values.Look(ref HoverCopyIncludeDebugContext, "HoverCopyIncludeDebugContext", true);
            Scribe_Values.Look(ref HoverCopyIncludeParentChain, "HoverCopyIncludeParentChain", true);
            Scribe_Values.Look(ref HoverCopyIncludeCursorPosition, "HoverCopyIncludeCursorPosition", true);
            Scribe_Values.Look(ref EnableTickSequenceRecording, "EnableTickSequenceRecording", false);
            Scribe_Values.Look(ref CopyTickSequenceSummaryToClipboard, "CopyTickSequenceSummaryToClipboard", true);
            Scribe_Values.Look(ref IncludeLiveGameLoopPhases, "IncludeLiveGameLoopPhases", true);
            Scribe_Values.Look(ref IncludeMapTickEvents, "IncludeMapTickEvents", true);
            Scribe_Values.Look(ref IncludeThingComponentTickEvents, "IncludeThingComponentTickEvents", true);
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
            EnableHoverDefinitionCopy = false;
            HoverCopyIncludeDebugContext = true;
            HoverCopyIncludeParentChain = true;
            HoverCopyIncludeCursorPosition = true;
            EnableTickSequenceRecording = false;
            CopyTickSequenceSummaryToClipboard = true;
            IncludeLiveGameLoopPhases = true;
            IncludeMapTickEvents = true;
            IncludeThingComponentTickEvents = true;

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

}
