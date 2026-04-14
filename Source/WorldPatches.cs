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

    
}
