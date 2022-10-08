using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace DVCareer
{
    static class DVCareer
    {
        public static UnityModManager.ModEntry modEntry;
        public static Settings settings;

        static void OnLoad(UnityModManager.ModEntry modEntry)
        {
            DVCareer.modEntry = modEntry;

            try { settings = Settings.Load<Settings>(modEntry); } catch { }
        }

        public static void Log(object message)
        {
            if (!settings.isLoggingEnabled) { return; }

            if (message is string) { modEntry.Logger.Log(message as string); }
            else
            {
                modEntry.Logger.Log("Logging object via UnityEngine.Debug...");
                Debug.Log(message);
            }
        }
        public static void LogWarning(object message) { modEntry.Logger.Warning($"{message}"); }
        public static void LogError(object message) { modEntry.Logger.Error($"{message}"); }

        public static void OnCriticalFailure()
        {
            // TODO: show floaty message (and offer to open log folder?) before quitting game
            modEntry.Active = false;
            modEntry.Logger.Critical("Deactivating mod DVCareer due to unrecoverable failure!");
            modEntry.Logger.Warning($"You can reactivate DVCareer by restarting the game, but this failure type likely indicates an incompatibility between the mod and a recent game update. Please search the mod's Github issue tracker for a relevant report. If none is found, please open one and include this log file.");
            Application.Quit();
        }
    }
}
