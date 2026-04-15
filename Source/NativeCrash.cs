using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class NativeCrashWriter
    {
        private const int ExceptionContinueSearch = 0;
        private const uint ExceptionAccessViolation = 0xC0000005;
        private const uint ExceptionArrayBoundsExceeded = 0xC000008C;
        private const uint ExceptionDatatypeMisalignment = 0x80000002;
        private const uint ExceptionIllegalInstruction = 0xC000001D;
        private const uint ExceptionInPageError = 0xC0000006;
        private const uint ExceptionPrivInstruction = 0xC0000096;
        private const uint ExceptionStackOverflow = 0xC00000FD;

        private static readonly object snapshotGate = new object();
        private static readonly NativeUnhandledExceptionFilterDelegate unhandledExceptionFilter = OnUnhandledExceptionFilter;
        private static readonly VectoredExceptionHandlerDelegate vectoredExceptionHandler = OnVectoredException;

        private static int hookInstallState;
        private static int nativeFaultWriteState;
        private static IntPtr vectoredHandlerHandle;
        private static NativeExceptionSnapshot lastNativeSnapshot;

        internal static string LastReportPath { get; private set; }
        internal static string LastDumpPath { get; private set; }

        internal static void InstallLowLevelHooks()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger.Message("Native crash hooks skipped on non-Windows platform.");
                return;
            }

            if (Interlocked.Exchange(ref hookInstallState, 1) == 1)
            {
                return;
            }

            var installedAny = false;

            try
            {
                vectoredHandlerHandle = AddVectoredExceptionHandler(1u, vectoredExceptionHandler);
                if (vectoredHandlerHandle != IntPtr.Zero)
                {
                    installedAny = true;
                }
                else
                {
                    Logger.Warning($"Native vectored exception handler install failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Native vectored exception handler install threw: {ex.Message}");
            }

            try
            {
                SetUnhandledExceptionFilter(unhandledExceptionFilter);
                installedAny = true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Native unhandled exception filter install threw: {ex.Message}");
            }

            if (installedAny)
            {
                Logger.Message("Native crash hooks installed.");
            }
            else
            {
                Logger.Warning("Native crash hooks could not be installed; continuing with managed-only fallback.");
            }
        }

        internal static void Write(Exception exception, string sourceKey)
        {
            try
            {
                var dir = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, "NativeCrash.log");
                Logger.WriteAllText(LastReportPath, BuildSummary(exception, sourceKey, dir, GetLastSnapshot()));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to write native crash summary");
            }
        }

        private static int OnVectoredException(IntPtr exceptionPointers)
        {
            try
            {
                var snapshot = CaptureSnapshot(exceptionPointers, "NativeVectoredExceptionHandler");
                if (snapshot.IsInteresting)
                {
                    SetLastSnapshot(snapshot);
                }
            }
            catch
            {
            }

            return ExceptionContinueSearch;
        }

        private static int OnUnhandledExceptionFilter(IntPtr exceptionPointers)
        {
            try
            {
                var snapshot = CaptureSnapshot(exceptionPointers, "NativeUnhandledExceptionFilter");
                SetLastSnapshot(snapshot);
                WriteImmediateFaultArtifacts(snapshot);

                if (!FirstTickGuard.crashLatched)
                {
                    CrashCrashHandler.Latch(snapshot.ToManagedException(), snapshot.SourceKey);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.Error(ex, "Native unhandled exception filter failed");
                }
                catch
                {
                }
            }

            return ExceptionContinueSearch;
        }

        private static void WriteImmediateFaultArtifacts(NativeExceptionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (Interlocked.Exchange(ref nativeFaultWriteState, 1) == 1)
            {
                return;
            }

            try
            {
                var dir = Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, "NativeCrash.log");
                Logger.WriteAllText(LastReportPath, BuildSummary(null, snapshot.SourceKey, dir, snapshot));
                TryWriteMiniDump(snapshot, dir);
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.Error(ex, "Failed to write immediate native crash artifacts");
                }
                catch
                {
                }
            }
        }

        private static void TryWriteMiniDump(NativeExceptionSnapshot snapshot, string reportDirectory)
        {
            if (snapshot == null || snapshot.ExceptionPointers == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var dumpPath = Path.Combine(reportDirectory, "NativeCrash.dmp");
                using (var stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    var process = Process.GetCurrentProcess();
                    var exceptionInfo = new MinidumpExceptionInformation
                    {
                        ThreadId = snapshot.ThreadId,
                        ExceptionPointers = snapshot.ExceptionPointers,
                        ClientPointers = false
                    };

                    var dumpType = MinidumpType.MiniDumpWithThreadInfo |
                                   MinidumpType.MiniDumpWithUnloadedModules |
                                   MinidumpType.MiniDumpWithIndirectlyReferencedMemory;

                    if (!MiniDumpWriteDump(process.Handle, process.Id, stream.SafeFileHandle.DangerousGetHandle(), dumpType, ref exceptionInfo, IntPtr.Zero, IntPtr.Zero))
                    {
                        Logger.Warning($"MiniDumpWriteDump failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                        return;
                    }
                }

                LastDumpPath = dumpPath;
            }
            catch (Exception ex)
            {
                Logger.Warning($"MiniDumpWriteDump threw: {ex.Message}");
            }
        }

        private static NativeExceptionSnapshot CaptureSnapshot(IntPtr exceptionPointers, string sourceKey)
        {
            if (exceptionPointers == IntPtr.Zero)
            {
                return NativeExceptionSnapshot.Empty(sourceKey);
            }

            var pointers = Marshal.PtrToStructure<ExceptionPointers>(exceptionPointers);
            var record = pointers.ExceptionRecord != IntPtr.Zero
                ? Marshal.PtrToStructure<ExceptionRecord>(pointers.ExceptionRecord)
                : default;

            return new NativeExceptionSnapshot(
                sourceKey,
                record.ExceptionCode,
                record.ExceptionAddress,
                record.NumberParameters,
                GetExceptionInformation(record, 0),
                GetExceptionInformation(record, 1),
                exceptionPointers,
                GetCurrentThreadId(),
                DateTime.Now,
                pointers.ContextRecord != IntPtr.Zero);
        }

        private static IntPtr GetExceptionInformation(ExceptionRecord record, int index)
        {
            if (record.ExceptionInformation == null || index < 0 || index >= record.ExceptionInformation.Length)
            {
                return IntPtr.Zero;
            }

            return record.ExceptionInformation[index];
        }

        private static void SetLastSnapshot(NativeExceptionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (snapshotGate)
            {
                lastNativeSnapshot = snapshot;
            }
        }

        private static NativeExceptionSnapshot GetLastSnapshot()
        {
            lock (snapshotGate)
            {
                return lastNativeSnapshot;
            }
        }

        private static string BuildSummary(Exception exception, string sourceKey, string reportDirectory, NativeExceptionSnapshot snapshot)
        {
            var builder = new StringBuilder();
            var process = Process.GetCurrentProcess();

            builder.AppendLine("CrashCatcher native crash summary");
            builder.AppendLine($"Generated: {DateTime.Now:O}");
            builder.AppendLine($"Source key: {sourceKey ?? "<null>"}");
            builder.AppendLine($"Classification: {Classify(sourceKey, snapshot)}");
            builder.AppendLine($"Program state: {Current.ProgramState}");
            builder.AppendLine($"Unity version: {Application.unityVersion}");
            builder.AppendLine($"Process: {process.ProcessName} ({process.Id})");
            builder.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
            builder.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            builder.AppendLine($"OS version: {Environment.OSVersion}");
            builder.AppendLine($"Command line: {Environment.CommandLine}");
            builder.AppendLine();
            builder.AppendLine("This is a best-effort low-level artifact written before or during CrashCatcher latch handling.");
            builder.AppendLine("It is meant to preserve process context for faults that may never surface through Unity logging.");

            if (snapshot != null && snapshot.IsInteresting)
            {
                builder.AppendLine();
                builder.AppendLine("--- Native exception ---");
                builder.AppendLine($"Code: 0x{snapshot.ExceptionCode:X8} ({snapshot.CodeDescription})");
                builder.AppendLine($"Address: {FormatPointer(snapshot.ExceptionAddress)}");
                builder.AppendLine($"Thread id: {snapshot.ThreadId}");
                builder.AppendLine($"Timestamp: {snapshot.Timestamp:O}");
                builder.AppendLine($"Has context record: {snapshot.HasContextRecord}");
                if (snapshot.ExceptionCode == ExceptionAccessViolation || snapshot.ExceptionCode == ExceptionInPageError)
                {
                    builder.AppendLine($"Access operation: {DescribeAccessOperation(snapshot.ExceptionInformation0)}");
                    builder.AppendLine($"Fault address: {FormatPointer(snapshot.ExceptionInformation1)}");
                }
                builder.AppendLine($"Exception pointers: {FormatPointer(snapshot.ExceptionPointers)}");
            }

            builder.AppendLine();
            builder.AppendLine("--- Exception ---");
            builder.AppendLine(exception?.ToString() ?? snapshot?.ToManagedException().ToString() ?? "<no exception provided>");
            builder.AppendLine();
            builder.AppendLine("--- Related artifacts ---");
            builder.AppendLine(Path.Combine(reportDirectory, "FirstTickCrash.log"));
            builder.AppendLine(Path.Combine(reportDirectory, "LoadPhase.log"));
            builder.AppendLine(Path.Combine(reportDirectory, "StackTrace.log"));
            if (!string.IsNullOrWhiteSpace(LastDumpPath))
            {
                builder.AppendLine(LastDumpPath);
            }

            return builder.ToString();
        }

        private static string DescribeAccessOperation(IntPtr operation)
        {
            switch (operation.ToInt64())
            {
                case 0:
                    return "read";
                case 1:
                    return "write";
                case 8:
                    return "execute";
                default:
                    return $"unknown ({operation})";
            }
        }

        private static string FormatPointer(IntPtr pointer)
        {
            return pointer == IntPtr.Zero ? "0x0" : $"0x{pointer.ToInt64():X}";
        }

        private static string Classify(string sourceKey, NativeExceptionSnapshot snapshot)
        {
            if (snapshot != null && snapshot.IsInteresting)
            {
                return snapshot.ExceptionCode == ExceptionAccessViolation ? "native-access-violation" : "native-unhandled-exception";
            }

            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return "unknown";
            }

            if (sourceKey.IndexOf("UnityLog", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "unity-log";
            }

            if (sourceKey.IndexOf("UnhandledException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceKey.IndexOf("FirstChance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "managed-exception";
            }

            return "best-effort-native-fallback";
        }

        private delegate int NativeUnhandledExceptionFilterDelegate(IntPtr exceptionPointers);
        private delegate int VectoredExceptionHandlerDelegate(IntPtr exceptionPointers);

        [Flags]
        private enum MinidumpType : uint
        {
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithUnloadedModules = 0x00000020
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinidumpExceptionInformation
        {
            public int ThreadId;
            public IntPtr ExceptionPointers;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ClientPointers;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExceptionPointers
        {
            public IntPtr ExceptionRecord;
            public IntPtr ContextRecord;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExceptionRecord
        {
            public uint ExceptionCode;
            public uint ExceptionFlags;
            public IntPtr InnerExceptionRecord;
            public IntPtr ExceptionAddress;
            public uint NumberParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public IntPtr[] ExceptionInformation;
        }

        private sealed class NativeExceptionSnapshot
        {
            internal string SourceKey { get; }
            internal uint ExceptionCode { get; }
            internal IntPtr ExceptionAddress { get; }
            internal uint NumberParameters { get; }
            internal IntPtr ExceptionInformation0 { get; }
            internal IntPtr ExceptionInformation1 { get; }
            internal IntPtr ExceptionPointers { get; }
            internal int ThreadId { get; }
            internal DateTime Timestamp { get; }
            internal bool HasContextRecord { get; }

            internal bool IsInteresting => ExceptionCode != 0 && (ExceptionCode == ExceptionAccessViolation ||
                                                                   ExceptionCode == ExceptionArrayBoundsExceeded ||
                                                                   ExceptionCode == ExceptionDatatypeMisalignment ||
                                                                   ExceptionCode == ExceptionIllegalInstruction ||
                                                                   ExceptionCode == ExceptionInPageError ||
                                                                   ExceptionCode == ExceptionPrivInstruction ||
                                                                   ExceptionCode == ExceptionStackOverflow);

            internal string CodeDescription
            {
                get
                {
                    switch (ExceptionCode)
                    {
                        case ExceptionAccessViolation:
                            return "EXCEPTION_ACCESS_VIOLATION";
                        case ExceptionArrayBoundsExceeded:
                            return "EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
                        case ExceptionDatatypeMisalignment:
                            return "EXCEPTION_DATATYPE_MISALIGNMENT";
                        case ExceptionIllegalInstruction:
                            return "EXCEPTION_ILLEGAL_INSTRUCTION";
                        case ExceptionInPageError:
                            return "EXCEPTION_IN_PAGE_ERROR";
                        case ExceptionPrivInstruction:
                            return "EXCEPTION_PRIV_INSTRUCTION";
                        case ExceptionStackOverflow:
                            return "EXCEPTION_STACK_OVERFLOW";
                        default:
                            return "UNKNOWN";
                    }
                }
            }

            internal NativeExceptionSnapshot(string sourceKey, uint exceptionCode, IntPtr exceptionAddress, uint numberParameters, IntPtr exceptionInformation0, IntPtr exceptionInformation1, IntPtr exceptionPointers, int threadId, DateTime timestamp, bool hasContextRecord)
            {
                SourceKey = sourceKey;
                ExceptionCode = exceptionCode;
                ExceptionAddress = exceptionAddress;
                NumberParameters = numberParameters;
                ExceptionInformation0 = exceptionInformation0;
                ExceptionInformation1 = exceptionInformation1;
                ExceptionPointers = exceptionPointers;
                ThreadId = threadId;
                Timestamp = timestamp;
                HasContextRecord = hasContextRecord;
            }

            internal Exception ToManagedException()
            {
                var message = $"Native crash detected by CrashCatcher. Code=0x{ExceptionCode:X8} ({CodeDescription}), Address={FormatPointer(ExceptionAddress)}, ThreadId={ThreadId}";
                return new AccessViolationException(message);
            }

            internal static NativeExceptionSnapshot Empty(string sourceKey)
            {
                return new NativeExceptionSnapshot(sourceKey, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, DateTime.Now, false);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr AddVectoredExceptionHandler(uint first, VectoredExceptionHandlerDelegate handler);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr SetUnhandledExceptionFilter(NativeUnhandledExceptionFilterDelegate filter);

        [DllImport("Dbghelp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            int processId,
            IntPtr hFile,
            MinidumpType dumpType,
            ref MinidumpExceptionInformation exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);
    }
}
