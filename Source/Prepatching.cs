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
                HybridMonitorLauncher.Start();
                HybridMonitorLauncher.Record("Static startup constructor entered.");
                var mod = LoadedModManager.GetMod<CrashCatcherMod>();
                var settings = mod?.GetSettings<CrashCatcherSettings>();
                if (settings != null && !settings.AutoArmEnabled)
                {
                    HybridMonitorLauncher.Record("Auto-arm disabled in static startup constructor.");
                    Log.Message("[CrashCatcher] Auto-arm disabled; running passive (no hooks installed).");
                    return;
                }

                HybridMonitorLauncher.Record("Static startup installing hooks.");
                CrashCatcherHooks.Install();
                var harmony = new Harmony("JellyCreative.CrashCatcher");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[CrashCatcher] Armed.");
                HybridMonitorLauncher.Record("Static startup complete.");
            }
            catch (Exception ex)
            {
                HybridMonitorLauncher.Record($"Static startup failed: {ex.Message}");
                Log.Error($"[CrashCatcher] Failed to initialize:\n{ex}");
            }
        }
    }
}
