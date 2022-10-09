using Harmony12;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace DVCareer
{
    public class CareerSaveManager : SingletonBehaviour<CareerSaveManager>
    {
        private static readonly string PRIMARY_SAVE_KEY = "DVCareer";
        private static readonly string VERSION_SAVE_KEY = "Version";

        public static new string AllowAutoCreate() { return "DVCareer_CareerSaveManager"; }

        [HarmonyPatch(typeof(SaveGameManager), "Save")]
        class SaveGameManager_Save_Patch
        {
            static void Prefix(SaveGameManager __instance)
            {
                try
                {
                    var tracksHash = SingletonBehaviour<CarsSaveManager>.Instance.TracksHash;

                    // TODO: save mod data

                    JObject saveData = new JObject(
                        new JProperty(VERSION_SAVE_KEY, new JValue(DVCareer.Version.ToString())));

                    SaveGameManager.data.SetJObject(PRIMARY_SAVE_KEY, saveData);
                }
                catch (Exception e) { DVCareer.OnCriticalFailure(e, "saving mod data"); }
            }
        }

        // CarsSaveManager.TracksHash, which is required, may not exist until CarsSaveManager.Load is finished
        // thus we patch it instead of another savegame data loading method
        [HarmonyPatch(typeof(CarsSaveManager), "Load")]
        class SaveGameManager_Load_Patch
        {
            static void Postfix(SaveGameManager __instance)
            {
                try
                {
                    JObject saveData = SaveGameManager.data.GetJObject(PRIMARY_SAVE_KEY);

                    if (saveData == null)
                    {
                        DVCareer.Log("Not loading save data: primary object is null.");
                        return;
                    }
                    var tracksHash = SingletonBehaviour<CarsSaveManager>.Instance.TracksHash;

                    // TODO: load mod data
                }
                catch (Exception e) { DVCareer.OnCriticalFailure(e, "loading mod data"); }
            }
        }
    }

}
