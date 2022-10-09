using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            // TODO: figure out if HasUpdate actually works; Intellisense displays "Not used"
            //if (modEntry.HasUpdate)
            //{
            //    NeedsUpdate();
            //    return;
            //}

            try { settings = Settings.Load<Settings>(modEntry); }
            catch {
                LogWarning("Unabled to load mod settings. Using defaults instead.");
                settings = new Settings();
            }

            try
            {
                var harmony = HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e) { OnCriticalFailure(e, "patching assembly"); }

            modEntry.OnUpdate = (entry, delta) =>
            {
                var unusedTrainCarDeleter = SingletonBehaviour<UnusedTrainCarDeleter>.Instance;
                if (unusedTrainCarDeleter != null)
                {
                    modEntry.OnUpdate = null;

                    try { unusedTrainCarDeleter.StopAllCoroutines(); }
                    catch (Exception e) { OnCriticalFailure(e, "stopping unused train car deleter");  }
                }
            };
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

        public static void OnCriticalFailure(Exception exception, string action)
        {
            // TODO: show floaty message (and offer to open log folder?) before quitting game
            modEntry.Active = false;
            Debug.Log(exception);
            modEntry.Logger.Critical("Deactivating mod DVCareer due to unrecoverable failure!");
            modEntry.Logger.Critical($"This happened while {action}.");
            modEntry.Logger.Critical($"You can reactivate DVCareer by restarting the game, but this failure type likely indicates an incompatibility between the mod and a recent game update. Please search the mod's Github issue tracker for a relevant report. If none is found, please open one and include this log file.");
            Application.Quit();
        }

        private static void NeedsUpdate()
        {
            // TODO: show floaty message before quitting game
            modEntry.Logger.Critical($"There is a new version of DVCareer available. Please install it to continue using the mod.\nInstalled: {modEntry.Version}\nLatest: {modEntry.NewestVersion}");
            Application.Quit();
        }
    }
}
