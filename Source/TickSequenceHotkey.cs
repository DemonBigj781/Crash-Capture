using HarmonyLib;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.HandleEventsHighPriority))]
    internal static class TickSequenceHotkeyPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            var current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type != EventType.KeyDown || !current.control || !current.shift || current.keyCode != KeyCode.X)
            {
                return;
            }

            if (TickSequenceRecorder.Start())
            {
                current.Use();
            }
        }
    }
}
