using DVOwnership.Patches;
using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace DVOwnership
{
    public static class DVOwnership
    {
        private static UnityModManager.ModEntry modEntry;
        private static HarmonyInstance harmony;

        public static Settings Settings { get; private set; }
        public static Version Version { get { return modEntry.Version; } }
        public static string DisplayName { get { return modEntry.Info.DisplayName; } }
        public static string Id { get { return modEntry.Info.Id; } }

        static void OnLoad(UnityModManager.ModEntry loadedEntry)
        {
            modEntry = loadedEntry;

            if (!modEntry.Enabled) { return; }

            // TODO: figure out if HasUpdate actually works; Intellisense displays "Not used"
            //if (modEntry.HasUpdate)
            //{
            //    NeedsUpdate();
            //    return;
            //}

            try { Settings = Settings.Load<Settings>(modEntry); }
            catch {
                LogWarning("Unabled to load mod settings. Using defaults instead.");
                Settings = new Settings();
            }

            try
            {
                harmony = HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e) { OnCriticalFailure(e, "patching miscellaneous assembly"); }

            try { CargoTypes_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching CargoTypes"); }

            try { IdGenerator_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching IdGenerator"); }

            try { Preferences_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching Preferences"); }

            try { StationLocoSpawner_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching StationLocoSpawner"); }

            try { StationProceduralJobsController_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching StationProceduralJobsController"); }

            try { TrainCar_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching TrainCar"); }

            try { UnusedTrainCarDeleter_Patches.Setup(); }
            catch (Exception e) { OnCriticalFailure(e, "patching UnusedTrainCarDeleter"); }
        }

        public static DynamicMethod Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
        {
            return harmony.Patch(original, prefix, postfix, transpiler);
        }

        public static void LogDebug(System.Func<object> messageFactory)
        {
            if (Settings.selectedLogLevel > LogLevel.Debug) { return; }

            var message = messageFactory();
            if (message is string) { modEntry.Logger.Log(message as string); }
            else
            {
                modEntry.Logger.Log("Logging object via UnityEngine.Debug...");
                Debug.Log(message);
            }
        }

        public static void Log(object message)
        {
            if (Settings.selectedLogLevel > LogLevel.Info) { return; }

            if (message is string) { modEntry.Logger.Log(message as string); }
            else
            {
                modEntry.Logger.Log("Logging object via UnityEngine.Debug...");
                Debug.Log(message);
            }
        }

        public static void LogWarning(object message)
        {
            if (Settings.selectedLogLevel > LogLevel.Warn) { return; }

            modEntry.Logger.Warning($"{message}");
        }

        public static void LogError(object message)
        {
            if (Settings.selectedLogLevel > LogLevel.Error) { return; }

            modEntry.Logger.Error($"{message}");
        }

        public static void OnCriticalFailure(Exception exception, string action)
        {
            // TODO: show floaty message (and offer to open log folder?) before quitting game
#if DEBUG
#else
            modEntry.Enabled = false;
#endif
            Debug.Log(exception);
            modEntry.Logger.Critical("Deactivating mod DVOwnership due to unrecoverable failure!");
            modEntry.Logger.Critical($"This happened while {action}.");
            modEntry.Logger.Critical($"You can reactivate DVOwnership by restarting the game, but this failure type likely indicates an incompatibility between the mod and a recent game update. Please search the mod's Github issue tracker for a relevant report. If none is found, please open one and include this log file.");
            Application.Quit();
        }

        private static void NeedsUpdate()
        {
            // TODO: show floaty message before quitting game
            modEntry.Logger.Critical($"There is a new version of DVOwnership available. Please install it to continue using the mod.\nInstalled: {modEntry.Version}\nLatest: {modEntry.NewestVersion}");
            Application.Quit();
        }
    }
}
