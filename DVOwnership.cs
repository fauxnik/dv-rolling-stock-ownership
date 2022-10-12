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
    static class DVOwnership
    {
        private static UnityModManager.ModEntry modEntry;
        private static HarmonyInstance harmony;

        public static Settings Settings { get; private set; }
        public static Version Version { get { return modEntry.Version; } }

        static void OnLoad(UnityModManager.ModEntry loadedEntry)
        {
            modEntry = loadedEntry;

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
                CargoTypes_Patches.Setup();
                Preferences_Patches.Setup();
            }
            catch (Exception e) { OnCriticalFailure(e, "patching assembly"); }
        }

        public static DynamicMethod Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
        {
            return harmony.Patch(original, prefix, postfix, transpiler);
        }

        public static void Log(object message)
        {
            if (!Settings.isLoggingEnabled) { return; }

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
