using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using Prepatcher;

namespace CrashCatcher.Prepatching
{
    public static class FreePatchEntry
    {
        [FreePatch]
        public static void Start(ModuleDefinition module)
        {
            HybridMonitorLauncher.Start();
            HybridMonitorLauncher.Record("FreePatchEntry.Start entered.");

            if (!AutoArmProbe.ShouldAutoArmEarly())
            {
                HybridMonitorLauncher.Record("Auto-arm disabled; running passive.");
                CrashCatcherBootstrap.MarkPrepatchSkipped();
                return;
            }

            HybridMonitorLauncher.Record("Auto-arm enabled; installing hooks.");
            CrashCatcherHooks.Install();
            CrashCatcherBootstrap.MarkPrepatchActive();
            HybridMonitorLauncher.Record("Prepatch bootstrap active.");
        }
    }

    internal static class AutoArmProbe
    {
        private const string ConfigFolderSuffix = "AppData\\LocalLow\\Ludeon Studios\\RimWorld by Ludeon Studios\\Config";

        internal static bool ShouldAutoArmEarly()
        {
            try
            {
                var configValue = TryReadAutoArmEnabledFromConfig();
                if (configValue.HasValue)
                {
                    return configValue.Value;
                }
            }
            catch
            {
            }

            // If we can't find settings yet (first run, missing file, etc), default to armed.
            return true;
        }

        private static bool? TryReadAutoArmEnabledFromConfig()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(profile))
            {
                return null;
            }

            var configDir = Path.Combine(profile, ConfigFolderSuffix);
            if (!Directory.Exists(configDir))
            {
                return null;
            }

            foreach (var file in Directory.EnumerateFiles(configDir, "Mod_*.xml"))
            {
                if (file.IndexOf("crashcatcher", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                // Settings file names can vary depending on packageId/class; parse by element name.
                using (var stream = File.OpenRead(file))
                {
                    var doc = XDocument.Load(stream, LoadOptions.None);
                    var element = doc.Descendants("AutoArmEnabled").FirstOrDefault();
                    if (element == null)
                    {
                        continue;
                    }

                    if (bool.TryParse(element.Value?.Trim(), out var enabled))
                    {
                        return enabled;
                    }
                }
            }

            return null;
        }
    }

    internal static class CrashCatcherBootstrap
    {
        internal static bool PrepatchActive { get; private set; }
        internal static bool PrepatchSkipped { get; private set; }

        internal static void MarkPrepatchActive()
        {
            PrepatchActive = true;
            Verse.Log.Message("[CrashCatcher] Prepatch bootstrap active.");
            HybridMonitorLauncher.Record("Prepatch bootstrap active.");
        }

        internal static void MarkPrepatchSkipped()
        {
            PrepatchSkipped = true;
            Verse.Log.Message("[CrashCatcher] Prepatch bootstrap skipped (auto-arm disabled).");
            HybridMonitorLauncher.Record("Prepatch bootstrap skipped (auto-arm disabled).");
        }
    }
}
