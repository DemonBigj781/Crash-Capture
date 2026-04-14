using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Verse;

namespace CrashCatcher
{
    internal static class PauseNotice
    {
        private static CrashNoticeWindow activeWindow;
        private static Exception pendingException;

        internal static void Queue(Exception exception)
        {
            pendingException = exception;
        }

        internal static void TryShow()
        {
            if (pendingException == null || activeWindow != null)
            {
                return;
            }

            if (Find.WindowStack == null || Find.TickManager == null)
            {
                return;
            }

            try
            {
                CrashBackdrop.EnsureVisible();
                var reportPath = CrashReportWriter.LastReportPath ?? Path.Combine(GenFilePaths.SaveDataFolderPath, "CrashCatcher", "FirstTickCrash.log");
                var message = pendingException.Message;
                if (message.Contains("Compress will not work", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("dimensions are not multiples of 4", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Texture '' has dimensions", StringComparison.OrdinalIgnoreCase))
                {
                    message = "CrashCatcher detected a texture compression problem and froze the game before the render could continue.";
                }

                var body = $"CrashCatcher intercepted the first world tick failure and held the game paused.\n\n{message}\n\nCrash report:\n{reportPath}\n\nThis notice cannot be dismissed. Use Close game if you want to exit.";

                activeWindow = new CrashNoticeWindow(body, reportPath);
                Find.WindowStack.Add(activeWindow);
                pendingException = null;
            }
            catch (Exception ex)
            {
                Log.Error($"[CrashCatcher] Failed to show pause notice:\n{ex}");
            }
        }
    }

    internal static class CrashBackdrop
    {
        private static CrashBackdropWindow activeBackdrop;

        internal static void EnsureVisible()
        {
            if (!FirstTickGuard.crashLatched)
            {
                return;
            }

            if (Find.WindowStack == null)
            {
                return;
            }

            if (activeBackdrop != null)
            {
                return;
            }

            activeBackdrop = new CrashBackdropWindow();
            Find.WindowStack.Add(activeBackdrop);
        }
    }

    internal sealed class CrashBackdropWindow : Window
    {
        private readonly float glitchSeed = (float)DateTime.UtcNow.Ticks % 1000f;

        internal CrashBackdropWindow()
        {
            forcePause = true;
            absorbInputAroundWindow = false;
            doCloseX = false;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            preventCameraMotion = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        public override void DoWindowContents(Rect inRect)
        {
            var time = Time.realtimeSinceStartup + glitchSeed;

            GUI.color = FirstTickGuard.FrozenTint;
            Widgets.DrawBoxSolid(inRect, GUI.color);

            GUI.color = new Color(0.35f, 0.5f, 0.9f, 0.08f);
            for (int i = 0; i < 4; i++)
            {
                float y = Mathf.Repeat(time * 52f + i * 43f, inRect.height);
                float height = 2f + Mathf.PingPong(time * 6f + i, 3f);
                Widgets.DrawBoxSolid(new Rect(0f, y, inRect.width, height), GUI.color);
            }

            GUI.color = new Color(1f, 0.2f, 0.35f, 0.05f);
            float bandY = Mathf.Repeat(time * 31f, inRect.height);
            Widgets.DrawBoxSolid(new Rect(0f, bandY, inRect.width, 4f), GUI.color);
            GUI.color = Color.white;
        }

        public override bool OnCloseRequest() => false;
        public override void OnCancelKeyPressed() { }
        public override void OnAcceptKeyPressed() { }
    }

    internal sealed class CrashNoticeWindow : Window
    {
        private readonly string body;
        private readonly string reportPath;
        private readonly float glitchSeed = (float)DateTime.UtcNow.Ticks % 1000f;

        internal CrashNoticeWindow(string body, string reportPath)
        {
            this.body = body;
            this.reportPath = reportPath;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = false;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            preventCameraMotion = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(720f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Text.Font = GameFont.Small;
            var wobble = Mathf.Sin((Time.realtimeSinceStartup + glitchSeed) * 8f) * 1.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            Widgets.Label(new Rect(wobble, y, inRect.width, inRect.height - 80f), body);
            GUI.color = Color.white;
            y = inRect.height - 40f;

            var halfWidth = (inRect.width - 10f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, halfWidth, 40f), "Open report folder"))
            {
                try
                {
                    var folder = Path.GetDirectoryName(reportPath);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CrashCatcher] Failed to open crash report folder:\n{ex}");
                }
            }

            if (Widgets.ButtonText(new Rect(halfWidth + 10f, y, halfWidth, 40f), "Close game"))
            {
                CrashCatcherHooks.RequestQuit();
            }
        }

        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            if (Find.WindowStack == null || Find.WindowStack.currentlyDrawnWindow != this)
            {
                return;
            }
            GUI.color = new Color(1f, 0.9f, 0.3f, 0.85f);
            var labelRect = new Rect(24f, 24f, UI.screenWidth - 48f, 24f);
            Widgets.Label(labelRect, "[CrashCatcher] Catcher active");
            GUI.color = Color.white;
        }

        public override bool OnCloseRequest()
        {
            return false;
        }

        public override void OnCancelKeyPressed()
        {
        }

        public override void OnAcceptKeyPressed()
        {
        }
    }
}

