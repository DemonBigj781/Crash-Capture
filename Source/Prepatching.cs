using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace CrashCatcher
{
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
}
