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
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class TickManagerDoSingleTickGuard
    {
        private static bool armed = true;

        public static void Prefix()
        {
            CallTrail.Record("tick", "TickManager.DoSingleTick");
            if (FirstTickGuard.crashLatched)
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
            TelemetryRecorder.RecordPhase("Entering live game loop");
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
            TelemetryRecorder.RecordPhase("Live game loop update");
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
            TelemetryRecorder.RecordPhase("Live map update");
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
            TelemetryRecorder.RecordPhase("Live map pre-tick");
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
            TelemetryRecorder.RecordPhase("Live map post-tick");
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
            TelemetryRecorder.RecordPhase("Live map component tick", map?.Parent?.Label ?? map?.ToString());
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
            TelemetryRecorder.RecordPhase("Live map component update", map?.Parent?.Label ?? map?.ToString());
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
            TelemetryRecorder.RecordPhase("Live map component finalize init", map?.Parent?.Label ?? map?.ToString());
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
            TelemetryRecorder.RecordPhase("Live map generated", map?.Parent?.Label ?? map?.ToString());
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
            TelemetryRecorder.RecordPhase("Live map removed", map?.Parent?.Label ?? map?.ToString());
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

    
}
