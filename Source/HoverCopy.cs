using System;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class HoverCopyState
    {
        private static string hoveredDefinition = string.Empty;
        private static string hoveredControlLabel = string.Empty;
        private static string hoveredSource = string.Empty;
        private static string hoveredWindow = string.Empty;
        private static string hoveredWindowType = string.Empty;
        private static string hoveredCursor = string.Empty;
        private static int hoveredUniqueId;

        internal static void CaptureButton(Rect rect, string label)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return;
                }

                if (!Mouse.IsOver(rect) && !DebugViewSettings.drawTooltipEdges)
                {
                    return;
                }

                hoveredControlLabel = label;
                hoveredDefinition = label;
                hoveredSource = "button";
                hoveredCursor = Event.current != null ? $"{Event.current.mousePosition.x:0.##}, {Event.current.mousePosition.y:0.##}" : string.Empty;

                var window = Find.WindowStack?.currentlyDrawnWindow;
                hoveredWindow = window?.optionalTitle ?? string.Empty;
                hoveredWindowType = window?.GetType().FullName ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Failed to capture hovered button:\n{ex}");
            }
        }

        internal static void Capture(Rect rect, TipSignal tip)
        {
            try
            {
                if (tip.textGetter == null && string.IsNullOrWhiteSpace(tip.text))
                {
                    return;
                }

                if (!Mouse.IsOver(rect) && !DebugViewSettings.drawTooltipEdges)
                {
                    return;
                }

                hoveredDefinition = ResolveTipText(tip);
                hoveredControlLabel = string.Empty;
                hoveredSource = "tooltip";
                hoveredUniqueId = tip.uniqueId;
                hoveredCursor = Event.current != null ? $"{Event.current.mousePosition.x:0.##}, {Event.current.mousePosition.y:0.##}" : string.Empty;

                var window = Find.WindowStack?.currentlyDrawnWindow;
                hoveredWindow = window?.optionalTitle ?? string.Empty;
                hoveredWindowType = window?.GetType().FullName ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Failed to capture hovered definition:\n{ex}");
            }
        }

        internal static bool TryCopyClipboard()
        {
            if (!CrashCatcherModSettingsAccessor.EnableHoverDefinitionCopy)
            {
                return false;
            }

            var payload = BuildPayload();
            try
            {
                GUIUtility.systemCopyBuffer = payload;
                Log.Message("[CrashCatcher] Hover definition copied to clipboard.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Failed to copy hovered definition:\n{ex}");
                return false;
            }
        }

        private static string BuildPayload()
        {
            if (string.IsNullOrWhiteSpace(hoveredDefinition))
            {
                return "Hovered UI element:\nNo hovered UI element found.";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Hovered UI element:");
            builder.AppendLine($"Definition: {hoveredDefinition}");
            if (!string.IsNullOrWhiteSpace(hoveredControlLabel))
            {
                builder.AppendLine($"Control label: {hoveredControlLabel}");
            }
            builder.AppendLine($"Source: {hoveredSource}");

            if (CrashCatcherModSettingsAccessor.HoverCopyIncludeDebugContext)
            {
                builder.AppendLine();
                builder.AppendLine("Debug context:");
                builder.AppendLine($"Hovered window: {hoveredWindow}");
                builder.AppendLine($"Hovered window type: {hoveredWindowType}");
                builder.AppendLine($"Tooltip id: {hoveredUniqueId}");
                if (CrashCatcherModSettingsAccessor.HoverCopyIncludeCursorPosition)
                {
                    builder.AppendLine($"Cursor position: {hoveredCursor}");
                }
                if (CrashCatcherModSettingsAccessor.HoverCopyIncludeParentChain)
                {
                    builder.AppendLine("Parent chain: <not available from current UI data>");
                }
            }

            return builder.ToString();
        }

        private static string ResolveTipText(TipSignal tip)
        {
            try
            {
                if (tip.textGetter != null)
                {
                    var resolved = tip.textGetter();
                    if (!resolved.NullOrEmpty())
                    {
                        return resolved;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Failed to resolve hovered tooltip text:\n{ex}");
            }

            return tip.text ?? string.Empty;
        }
    }

    [HarmonyPatch(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion), new[] { typeof(Rect), typeof(TipSignal) })]
    internal static class TooltipHandlerTipRegionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Rect rect, TipSignal tip)
        {
            HoverCopyState.Capture(rect, tip);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(Color), typeof(bool), typeof(TextAnchor?) })]
    internal static class WidgetsButtonTextCapturePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Rect rect, string label)
        {
            HoverCopyState.CaptureButton(rect, label);
        }
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.HandleEventsHighPriority))]
    internal static class WindowStackHandleEventsHighPriorityPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            var current = Event.current;
            if (current == null || current.type != EventType.KeyDown || !current.control || !current.shift || current.keyCode != KeyCode.C)
            {
                return;
            }

            if (HoverCopyState.TryCopyClipboard())
            {
                current.Use();
            }
        }
    }

    internal static class CrashCatcherModSettingsAccessor
    {
        internal static bool EnableHoverDefinitionCopy => CurrentSettings?.EnableHoverDefinitionCopy ?? false;
        internal static bool HoverCopyIncludeDebugContext => CurrentSettings?.HoverCopyIncludeDebugContext ?? true;
        internal static bool HoverCopyIncludeParentChain => CurrentSettings?.HoverCopyIncludeParentChain ?? true;
        internal static bool HoverCopyIncludeCursorPosition => CurrentSettings?.HoverCopyIncludeCursorPosition ?? true;
        internal static bool EnableTickSequenceRecording => CurrentSettings?.EnableTickSequenceRecording ?? false;
        internal static bool CopyTickSequenceSummaryToClipboard => CurrentSettings?.CopyTickSequenceSummaryToClipboard ?? true;
        internal static bool IncludeLiveGameLoopPhases => CurrentSettings?.IncludeLiveGameLoopPhases ?? true;
        internal static bool IncludeMapTickEvents => CurrentSettings?.IncludeMapTickEvents ?? true;
        internal static bool IncludeThingComponentTickEvents => CurrentSettings?.IncludeThingComponentTickEvents ?? true;

        private static CrashCatcherSettings CurrentSettings => LoadedModManager.GetMod<CrashCatcherMod>()?.GetSettings<CrashCatcherSettings>();
    }
}
