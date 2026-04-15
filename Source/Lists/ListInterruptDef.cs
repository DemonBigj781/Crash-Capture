namespace CrashCatcher
{
    internal static class ListInterruptDef
    {
        internal static readonly string[] Ordered =
        {
            "CrashCrashHandler.Latch",
            "CrashCatcherHooks.OnFirstChanceException",
            "CrashCatcherHooks.OnUnhandledException",
            "CrashCatcherHooks.OnLogMessageReceivedThreaded",
            "CrashCatcherHooks.TryLatchPendingTextureWarning",
            "NativeCrashWriter.InstallLowLevelHooks",
            "NativeCrashWriter.Write",
            "Verse.Root.Update",
            "Verse.Game.UpdatePlay",
            "Verse.LongEventHandler.ExecuteWhenFinished",
            "Verse.Map.MapUpdate",
            "Verse.DynamicDrawManager.DrawDynamicThings",
            "Verse.PawnRenderer.DynamicDrawPhaseAt",
            "Verse.Thing.DoTick",
            "Verse.MapComponent.MapComponentTick",
            "Verse.AI.JobDriver.DriverTick"
        };
    }
}
