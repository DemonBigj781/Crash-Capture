using Mono.Cecil;
using Prepatcher;

namespace CrashCatcher.Prepatching
{
    public static class FreePatchEntry
    {
        [FreePatch]
        public static void Start(ModuleDefinition module)
        {
            CrashCatcherHooks.Install();
            CrashCatcherBootstrap.MarkPrepatchActive();
        }
    }

    internal static class CrashCatcherBootstrap
    {
        internal static bool PrepatchActive { get; private set; }

        internal static void MarkPrepatchActive()
        {
            PrepatchActive = true;
            Verse.Log.Message("[CrashCatcher] Prepatch bootstrap active.");
        }
    }
}
