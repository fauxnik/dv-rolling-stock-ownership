using DV.JObjectExtstensions;
using Harmony12;
using Newtonsoft.Json.Linq;
using System;

namespace DVCareer
{
    public class CareerSaveManager : SingletonBehaviour<CareerSaveManager>
    {
        private static readonly string PRIMARY_SAVE_KEY = "DVCareer";
        private static readonly string VERSION_SAVE_KEY = "Version";
        private static readonly string TRACKS_HASH_SAVE_KEY = "TracksHash";
        private static readonly string ROLLING_STOCK_SAVE_KEY = "RollingStock";

        public static new string AllowAutoCreate() { return "DVCareer_CareerSaveManager"; }

        [HarmonyPatch(typeof(SaveGameManager), "Save")]
        class SaveGameManager_Save_Patch
        {
            static void Prefix(SaveGameManager __instance)
            {
                try
                {
                    var tracksHash = SingletonBehaviour<CarsSaveManager>.Instance.TracksHash;
                    var rollingStockManager = SingletonBehaviour<RollingStockManager>.Instance;

                    // TODO: save more data

                    JObject saveData = new JObject(
                        new JProperty(VERSION_SAVE_KEY, DVCareer.Version.ToString()),
                        new JProperty(TRACKS_HASH_SAVE_KEY, tracksHash),
                        new JProperty(ROLLING_STOCK_SAVE_KEY, rollingStockManager.GetSaveData()));

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
                    var loadedTracksHash = saveData.GetString(TRACKS_HASH_SAVE_KEY);

                    var rollingStockSaveData = saveData[ROLLING_STOCK_SAVE_KEY];
                    if (rollingStockSaveData == null) { DVCareer.Log($"Not loading rolling stock; data is null."); }
                    else if (!(rollingStockSaveData.Type == JTokenType.Array)) { throw new Exception($"Tried to load rolling stock, but data type is {rollingStockSaveData.Type}; expected {JTokenType.Array}."); }
                    else if (loadedTracksHash != tracksHash)
                    {
                        // TODO: handle tracks hash change
                    }
                    else
                    {
                        var rollingStockManager = SingletonBehaviour<RollingStockManager>.Instance;
                        rollingStockManager.LoadSaveData(rollingStockSaveData as JArray);
                    }

                    // TODO: load more data
                }
                catch (Exception e) { DVCareer.OnCriticalFailure(e, "loading mod data"); }
            }
        }
    }

}
