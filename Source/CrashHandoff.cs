using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Text.RegularExpressions;
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
            NativeCrashWriter.Write(exception, sourceKey);
            CrashReportWriter.Write(exception);
            PauseNotice.Queue(exception);
            CrashCatcherHooks.OpenWatchdogGate();
            PauseNotice.TryShow();
            Logger.Error(exception, $"Crash latched from {sourceKey}");
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
            NativeCrashWriter.InstallLowLevelHooks();
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.wantsToQuit += OnWantsToQuit;
            Logger.Message("Log hook installed.");
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

        private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            if (FirstTickGuard.crashLatched || e?.Exception == null)
            {
                return;
            }

            var sourceKey = CrashCatcherFilters.ResolveKeyFromStack();
            if (string.IsNullOrWhiteSpace(sourceKey) || string.Equals(sourceKey, "UnknownDetector", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!CrashCatcherFilters.ShouldLatch(sourceKey, e.Exception))
            {
                return;
            }

            CallTrail.Record("first-chance", sourceKey, e.Exception.GetType().Name);
            CrashCrashHandler.Latch(e.Exception, sourceKey);
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
                Logger.Warning($"Reflection-only resolve failed for {args.Name}: {ex.Message}");
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
            Logger.Message("Close game requested.");
            Application.Quit();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(1500);
                    if (Interlocked.Exchange(ref quitRequestArmed, 0) == 1)
                    {
                        Logger.Warning("Close game fallback forcing process exit.");
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
            Logger.Message("Quit blocked by fail-safe latch.");
            return false;
        }

        private static void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (FirstTickGuard.crashLatched)
            {
                return;
            }

            var isTextureCompressionWarning = IsMeaningfulTextureCompressionWarning(condition);

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

        private static bool IsMeaningfulTextureCompressionWarning(string condition)
        {
            if (condition.NullOrEmpty())
            {
                return false;
            }

            if (!condition.Contains("Compress will not work", StringComparison.OrdinalIgnoreCase) &&
                !condition.Contains("dimensions are not multiples of 4", StringComparison.OrdinalIgnoreCase) &&
                !condition.Contains("Texture", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (condition.Contains("Texture '' has dimensions", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var match = Regex.Match(condition, @"Texture\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return !string.IsNullOrWhiteSpace(match.Groups[1].Value);
            }

            return condition.Contains("dimensions are not multiples of 4", StringComparison.OrdinalIgnoreCase) ||
                   condition.Contains("Compress will not work", StringComparison.OrdinalIgnoreCase);
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
                Logger.WriteAllText(LastReportPath, builder.ToString());
                CrashCatcherListWriter.Write(dir);
                StackTraceWriter.Write(dir);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write crash report");
            }
        }
    }

    internal static class CrashCatcherListWriter
    {
        internal static string LastReportPath { get; private set; }

        internal static void Write(string dir)
        {
            try
            {
                LastReportPath = Path.Combine(dir, "Lists.log");
                var builder = new System.Text.StringBuilder();
                builder.AppendLine("CrashCatcher shared lists snapshot");
                builder.AppendLine($"Generated: {DateTime.Now:O}");
                builder.AppendLine();
                builder.AppendLine("--- Source lists ---");
                foreach (var line in CrashCatcherSourceLists.Ordered)
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine();
                builder.AppendLine("--- Telemetry defs ---");
                foreach (var line in ListTelemetryDefs.Ordered)
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine();
                builder.AppendLine("--- Crash handoff markers ---");
                foreach (var line in CrashHandoffMarkers.Ordered)
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine();
                builder.AppendLine("--- Interrupt defs ---");
                foreach (var line in ListInterruptDef.Ordered)
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine();
                builder.AppendLine("--- Tick capture priority ---");
                foreach (var line in TickCapturePriority.Ordered)
                {
                    builder.AppendLine(line);
                }
                Logger.WriteAllText(LastReportPath, builder.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write list snapshot");
            }
        }
    }

    internal static class StackTraceWriter
    {
        internal static string LastReportPath { get; private set; }

        internal static void Write(string dir)
        {
            try
            {
                LastReportPath = Path.Combine(dir, "StackTrace.log");
                var builder = new System.Text.StringBuilder();
                builder.AppendLine("CrashCatcher stack trace snapshot");
                builder.AppendLine($"Generated: {DateTime.Now:O}");
                builder.AppendLine();
                builder.AppendLine("--- Last 100 calls ---");
                builder.AppendLine(CallTrail.Dump());
                Logger.WriteAllText(LastReportPath, builder.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write stack trace snapshot");
            }
        }
    }

}
