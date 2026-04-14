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
            TickSequenceRecorder.Update();
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

    
}
