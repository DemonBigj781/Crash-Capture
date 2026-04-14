using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrashCatcher
{

    public static class FirstTickGuard
    {
        public static bool crashLatched;
        public static readonly Color FrozenTint = new Color(0.07f, 0.08f, 0.1f, 0.92f);
    }

    internal static class CrashCrashHandler
    {
        internal static void Latch(Exception exception)
        {
            Latch(exception, CrashCatcherFilters.ResolveKeyFromStack());
        }

        internal static void Latch(Exception exception, string sourceKey)
        {
            if (exception == null)
            {
                exception = new Exception("[CrashCatcher] Unknown exception");
            }

            if (!CrashCatcherFilters.ShouldLatch(sourceKey, exception))
            {
                return;
            }

            if (FirstTickGuard.crashLatched)
            {
                return;
            }

            FirstTickGuard.crashLatched = true;
            TickSequenceRecorder.MarkHandedOff(sourceKey, exception);
            CrashBackdrop.EnsureVisible();
            CrashReportWriter.Write(exception);
            PauseNotice.Queue(exception);
            CrashCatcherHooks.OpenWatchdogGate();
            PauseNotice.TryShow();
            Log.Error($"[CrashCatcher] Crash latched from {sourceKey}:\n{exception}");
        }
    }

    internal static class CrashCatcherHooks
    {
        private static bool installed;
        private static ManualResetEventSlim crashLatchGate;
        private static Thread watchdogThread;
        private static readonly object pendingTextureWarningGate = new object();
        private static string pendingTextureWarning;
        private static int quitRequestArmed;

        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;
            crashLatchGate = new ManualResetEventSlim(false);
            watchdogThread = new Thread(WatchdogLoop)
            {
                IsBackground = true,
                Name = "CrashCatcher Watchdog"
            };
            watchdogThread.Start();
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.wantsToQuit += OnWantsToQuit;
            Log.Message("[CrashCatcher] Log hook installed.");
        }

        private static void WatchdogLoop()
        {
            try
            {
                crashLatchGate.Wait();
                while (true)
                {
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            catch
            {
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                CrashCrashHandler.Latch(exception, "UnhandledException");
            }
        }

        private static Assembly OnReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var requested = new AssemblyName(args.Name);
                var managedDir = GetManagedDirectory();
                if (managedDir == null)
                {
                    return null;
                }

                var candidate = Path.Combine(managedDir, requested.Name + ".dll");
                if (File.Exists(candidate))
                {
                    return Assembly.ReflectionOnlyLoadFrom(candidate);
                }

                var loaded = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in loaded)
                {
                    try
                    {
                        var location = asm.Location;
                        if (string.IsNullOrEmpty(location))
                        {
                            continue;
                        }

                        if (string.Equals(Path.GetFileNameWithoutExtension(location), requested.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return Assembly.ReflectionOnlyLoadFrom(location);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CrashCatcher] Reflection-only resolve failed for {args.Name}: {ex.Message}");
            }

            return null;
        }

        internal static void OpenWatchdogGate()
        {
            crashLatchGate?.Set();
        }

        internal static void RequestQuit()
        {
            Interlocked.Exchange(ref quitRequestArmed, 1);
            Log.Message("[CrashCatcher] Close game requested.");
            Application.Quit();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(1500);
                    if (Interlocked.Exchange(ref quitRequestArmed, 0) == 1)
                    {
                        Log.Warning("[CrashCatcher] Close game fallback forcing process exit.");
                        Environment.Exit(0);
                    }
                }
                catch
                {
                    try
                    {
                        Environment.Exit(0);
                    }
                    catch
                    {
                    }
                }
            });
        }

        private static bool OnWantsToQuit()
        {
            if (!FirstTickGuard.crashLatched)
            {
                return true;
            }

            if (Interlocked.Exchange(ref quitRequestArmed, 0) == 1)
            {
                return true;
            }

            CrashBackdrop.EnsureVisible();
            PauseNotice.TryShow();
            Log.Message("[CrashCatcher] Quit blocked by fail-safe latch.");
            return false;
        }

        private static void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (FirstTickGuard.crashLatched)
            {
                return;
            }

            var isTextureCompressionWarning =
                condition.Contains("Compress will not work", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("dimensions are not multiples of 4", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("Texture '' has dimensions", StringComparison.OrdinalIgnoreCase);

            if (type == LogType.Warning && !isTextureCompressionWarning && !CrashCatcherFilters.IsEnabled("UnityLogWarnings"))
            {
                return;
            }

            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert && !isTextureCompressionWarning && !CrashCatcherFilters.IsEnabled("UnityLogWarnings"))
            {
                return;
            }

            if (condition.NullOrEmpty())
            {
                return;
            }

            if (condition.StartsWith("[CrashCatcher]"))
            {
                return;
            }

            if (isTextureCompressionWarning)
            {
                if (!CrashCatcherFilters.IsEnabled("TextureWarningEscalation"))
                {
                    CallTrail.Record("filter", "TextureWarningEscalation", "disabled");
                    return;
                }

                lock (pendingTextureWarningGate)
                {
                    pendingTextureWarning = condition;
                }

                TelemetryRecorder.RecordPhase("Unity log texture warning", condition);
                CallTrail.Record("log", "UnityLog", "TextureWarningEscalation");
                var textureException = new Exception($"Texture warning triggered crash catcher.\n{condition}\n{stackTrace}");
                CrashCrashHandler.Latch(textureException, "TextureWarningEscalation");
                return;
            }

            if (type == LogType.Warning)
            {
                TelemetryRecorder.RecordPhase("Unity log warning", condition);
                CallTrail.Record("log", "UnityLog", "UnityLogWarnings");
                var warningException = new Exception($"Unity warning triggered crash catcher.\n{condition}\n{stackTrace}");
                CrashCrashHandler.Latch(warningException, "UnityLogWarnings");
                return;
            }

            TelemetryRecorder.RecordPhase("Unity log hard error", condition);
            CallTrail.Record("log", "UnityLog", "UnityLogHardErrors");
            var hardException = new Exception($"Unity log triggered crash catcher.\n{condition}\n{stackTrace}");
            CrashCrashHandler.Latch(hardException, "UnityLogHardErrors");
        }

        internal static void TryLatchPendingTextureWarning()
        {
            if (FirstTickGuard.crashLatched)
            {
                return;
            }

            string warning;
            lock (pendingTextureWarningGate)
            {
                warning = pendingTextureWarning;
                pendingTextureWarning = null;
            }

            if (warning.NullOrEmpty())
            {
                return;
            }

            var exception = new Exception($"Texture warning escalated inside the game loop.\n{warning}");
            CrashCrashHandler.Latch(exception, "TextureWarningEscalation");
        }

        private static string GetManagedDirectory()
        {
            try
            {
                var dataPath = UnityData.dataPath;
                if (string.IsNullOrWhiteSpace(dataPath))
                {
                    return null;
                }

                var rootDir = Directory.GetParent(dataPath);
                if (rootDir == null)
                {
                    return null;
                }

                var managedDir = Path.Combine(rootDir.FullName, "RimWorldWin64_Data", "Managed");
                return Directory.Exists(managedDir) ? managedDir : null;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class CrashReportWriter
    {
        internal static string LastReportPath { get; private set; }

        internal static void Write(Exception exception)
        {
            try
            {
                var dir = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, "FirstTickCrash.log");
                var builder = new System.Text.StringBuilder();
                builder.AppendLine(exception.ToString());
                builder.AppendLine();
                builder.AppendLine("--- Input / menu telemetry ---");
                foreach (var line in TelemetryRecorder.SnapshotInputs())
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine();
                builder.AppendLine("--- Load phase telemetry ---");
                foreach (var line in TelemetryRecorder.SnapshotPhases())
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine();
                builder.AppendLine("--- Last 100 calls ---");
                builder.AppendLine(CallTrail.Dump());
                File.WriteAllText(LastReportPath, builder.ToString());
            }
            catch (Exception ex)
            {
                Log.Error($"[CrashCatcher] Failed to write crash report:\n{ex}");
            }
        }
    }

}
