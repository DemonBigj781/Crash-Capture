using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace CrashCatcher
{
    internal static class HybridMonitorPaths
    {
        internal static string ResolveReportDirectory()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(profile))
            {
                return null;
            }

            return Path.Combine(profile, "AppData", "LocalLow", "Ludeon Studios", "RimWorld by Ludeon Studios", "CrashCatcher");
        }

        internal static string ResolvePlayerLogPath()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(profile))
            {
                return null;
            }

            return Path.Combine(profile, "AppData", "LocalLow", "Ludeon Studios", "RimWorld by Ludeon Studios", "Player.log");
        }
    }

    internal static class HybridMonitorBreadcrumbs
    {
        private static readonly object gate = new object();

        internal static void RecordPrepatch(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var reportDir = HybridMonitorPaths.ResolveReportDirectory();
            if (string.IsNullOrWhiteSpace(reportDir))
            {
                return;
            }

            try
            {
                lock (gate)
                {
                    Directory.CreateDirectory(reportDir);
                    Logger.AppendAllText(
                        Path.Combine(reportDir, "Prepatch.log"),
                        $"{DateTime.UtcNow:O} | {message}{Environment.NewLine}");
                }
            }
            catch
            {
            }

            try
            {
                CallTrail.Record("prepatch", "CrashCatcher.Prepatch", message);
            }
            catch
            {
            }
        }
    }

    internal static class HybridMonitorLauncher
    {
        private static readonly object gate = new object();
        private static int started;
        private static int helperProcessId;

        internal static void Start()
        {
            if (Interlocked.CompareExchange(ref started, 1, 0) != 0)
            {
                return;
            }

            var reportDir = HybridMonitorPaths.ResolveReportDirectory();
            var playerLogPath = HybridMonitorPaths.ResolvePlayerLogPath();
            if (string.IsNullOrWhiteSpace(reportDir))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(reportDir);
                HybridMonitorBreadcrumbs.RecordPrepatch("Hybrid monitor bootstrap entered.");
                StartHelperProcess(reportDir, playerLogPath);
                HybridMonitorBreadcrumbs.RecordPrepatch("Hybrid monitor helper launch attempted.");
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref started, 0);
                Logger.Warning($"Hybrid monitor startup failed: {ex.Message}");
                HybridMonitorBreadcrumbs.RecordPrepatch($"Hybrid monitor startup failed: {ex.Message}");
            }
        }

        internal static void Record(string message)
        {
            HybridMonitorBreadcrumbs.RecordPrepatch(message);
        }

        private static void StartHelperProcess(string reportDir, string playerLogPath)
        {
            lock (gate)
            {
                if (helperProcessId != 0)
                {
                    return;
                }

                var script = BuildPowerShellScript();
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName = ResolvePowerShellPath(),
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = reportDir
                };

                psi.EnvironmentVariables["CRASHCATCHER_REPORT_DIR"] = reportDir;
                psi.EnvironmentVariables["CRASHCATCHER_GAME_PID"] = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
                psi.EnvironmentVariables["CRASHCATCHER_PLAYER_LOG"] = playerLogPath ?? string.Empty;

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        helperProcessId = process.Id;
                    }
                }
            }
        }

        private static string ResolvePowerShellPath()
        {
            try
            {
                var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrWhiteSpace(windowsDir))
                {
                    var candidate = Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return "powershell.exe";
        }

        private static string BuildPowerShellScript()
        {
            return @"
$ErrorActionPreference = 'Stop'
$reportDir = $env:CRASHCATCHER_REPORT_DIR
$gamePid = [int]$env:CRASHCATCHER_GAME_PID
$playerLog = $env:CRASHCATCHER_PLAYER_LOG
$prepatchPath = Join-Path $reportDir 'Prepatch.log'
$heartbeatPath = Join-Path $reportDir 'LoggerHeartbeat.log'
$monitorPath = Join-Path $reportDir 'ExternalMonitor.log'

function Write-Line([string]$path, [string]$text) {
    Add-Content -LiteralPath $path -Value $text -Encoding UTF8
}

function Stamp([string]$text) {
    '{0:o} | {1}' -f [DateTime]::UtcNow, $text
}

Write-Line $prepatchPath (Stamp 'external helper started')
Write-Line $monitorPath (Stamp ('watching pid=' + $gamePid))

$lastHeartbeat = [DateTime]::UtcNow.AddMinutes(-1)
$lastLogSize = -1

while ($true) {
    try {
        $proc = Get-Process -Id $gamePid -ErrorAction SilentlyContinue
        if (-not $proc) {
            break
        }

        if (([DateTime]::UtcNow - $lastHeartbeat).TotalSeconds -ge 2) {
            Write-Line $heartbeatPath (Stamp ('alive pid=' + $gamePid))
            $lastHeartbeat = [DateTime]::UtcNow
        }

        if ($playerLog -and (Test-Path -LiteralPath $playerLog)) {
            $item = Get-Item -LiteralPath $playerLog -ErrorAction SilentlyContinue
            if ($item -and $item.Length -ne $lastLogSize) {
                $lastLogSize = $item.Length
                Write-Line $monitorPath (Stamp ('player log size=' + $lastLogSize))
            }
        }

        Start-Sleep -Milliseconds 250
    }
    catch {
        Write-Line $monitorPath (Stamp ('monitor error: ' + $_.Exception.Message))
        Start-Sleep -Seconds 1
    }
}

Write-Line $prepatchPath (Stamp 'external helper exiting')
Write-Line $monitorPath (Stamp 'target process exited')
";
        }
    }
}
