using System;
using System.IO;
using UnityEngine;

namespace Fortal.Display
{
    // Keeps the log of a run that crashed. Unity overwrites Player.log on every start (keeping only
    // one older copy), so after a crash and a couple of restarts the reason is gone. A marker file is
    // written when the game starts and removed when it quits properly; if the marker is still there
    // at the next start, the last run crashed (or was killed), and its log (now Player-prev.log) is
    // copied to "Crash Logs" next to it, named by the time it was found. Builds only.
    public static class CrashLogKeeper
    {
#if !UNITY_EDITOR
        private const string MarkerName = "running.marker";
        private const string FolderName = "Crash Logs";
        private const string PreviousLogName = "Player-prev.log";

        private static string markerPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Begin()
        {
            try
            {
                string logPath = Application.consoleLogPath;
                if (string.IsNullOrEmpty(logPath)) return;

                string folder = Path.GetDirectoryName(logPath);
                markerPath = Path.Combine(folder, MarkerName);
                if (File.Exists(markerPath)) KeepPreviousLog(folder);

                File.WriteAllText(markerPath, DateTime.Now.ToString("o"));
                Application.quitting += OnQuitting;
            }
            catch (Exception fault)
            {
                // Keeping logs must never stop the game from starting.
                Debug.LogWarning($"[CrashLogKeeper] Could not set up: {fault.Message}");
            }
        }

        private static void KeepPreviousLog(string folder)
        {
            string previous = Path.Combine(folder, PreviousLogName);
            if (!File.Exists(previous)) return;

            string kept = Path.Combine(folder, FolderName);
            Directory.CreateDirectory(kept);
            string copy = Path.Combine(kept, $"crash {DateTime.Now:yyyy-MM-dd HH-mm-ss}.log");
            File.Copy(previous, copy, true);
            Debug.LogWarning($"[CrashLogKeeper] The last run did not quit properly; its log was kept at {copy}");
        }

        private static void OnQuitting()
        {
            try
            {
                if (markerPath != null && File.Exists(markerPath)) File.Delete(markerPath);
            }
            catch (Exception)
            {
                // Quitting anyway; at worst the next start keeps a log it did not need to.
            }
        }
#endif
    }
}
