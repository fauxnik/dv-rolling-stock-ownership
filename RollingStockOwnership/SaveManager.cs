using DV;
using DV.JObjectExtstensions;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;

namespace RollingStockOwnership;

public class SaveManager
{
	private const string PRIMARY_SAVE_KEY = "RollingStockOwnership";
	private const string VERSION_SAVE_KEY = "Version";
	private const string TRACKS_HASH_SAVE_KEY = "TracksHash";
	private const string ROLLING_STOCK_SAVE_KEY = "RollingStock";
	private const string RESERVATION_SAVE_KEY = "Reservations";

	[HarmonyPatch(typeof(SaveGameManager), "Save")]
	class SaveGameManager_Save_Patch
	{
		static void Prefix(SaveGameManager __instance)
		{

			try
			{
				var tracksHash = WorldData.Instance.TracksHash;
				var rollingStockManager = RollingStockManager.Instance;
				var reservationManager = ReservationManager.Instance;
				SaveGameManager saveGameManager = SaveGameManager.Instance;

				// TODO: is there any more data that needs to be saved?

				JObject saveData = new JObject(
					new JProperty(VERSION_SAVE_KEY, Main.Version.ToString()),
					new JProperty(TRACKS_HASH_SAVE_KEY, tracksHash),
					new JProperty(ROLLING_STOCK_SAVE_KEY, rollingStockManager.GetSaveData()),
					new JProperty(RESERVATION_SAVE_KEY, reservationManager.GetSaveData()));

				saveGameManager.data.SetJObject(PRIMARY_SAVE_KEY, saveData);
			}
			catch (Exception e) { Main.OnCriticalFailure(e, "saving mod data"); }
		}
	}

	// TODO: WorldData.Instance.TracksHash is the new version of CarsSaveManager.TracksHash,
	//       and it exists from the start. Should this be updated to patch a different method now?
	// CarsSaveManager.TracksHash, which is required, may not exist until CarsSaveManager.Load is finished
	// thus we patch it instead of another savegame data loading method
	[HarmonyPatch(typeof(CarsSaveManager), "Load")]
	class SaveGameManager_Load_Patch
	{

		static void Postfix(SaveGameManager __instance)
		{
			SaveGameManager saveGameManager = SaveGameManager.Instance;
			try
			{
				// Any previously loaded data must be cleared before loading new data
				RollingStockManager.Instance.Clear();

				JObject saveData = saveGameManager.data.GetJObject(PRIMARY_SAVE_KEY);

				// Data migration for old save data
				saveData ??= saveGameManager.data.GetJObject("DVOwnership");

				if (saveData == null)
				{
					Main.Log("Not loading save data: primary object is null.");
					return;
				}
				var tracksHash = WorldData.Instance.TracksHash;
				var loadedTracksHash = saveData.GetString(TRACKS_HASH_SAVE_KEY);

				var rollingStockSaveData = saveData[ROLLING_STOCK_SAVE_KEY];
				if (rollingStockSaveData == null) { Main.Log($"Not loading rolling stock: data is null."); }
				else if (!(rollingStockSaveData is JArray rollingStockJArray)) { throw new Exception($"Tried to load rolling stock, but data type is {rollingStockSaveData.Type} instead of {JTokenType.Array}."); }
				else if (loadedTracksHash != tracksHash)
				{
					// TODO: handle tracks hash change
				}
				else
				{
					var rollingStockManager = RollingStockManager.Instance;
					rollingStockManager.LoadSaveData(rollingStockJArray);

					var reservationsSaveData = saveData[RESERVATION_SAVE_KEY];
					if (reservationsSaveData == null) { Main.Log($"Not loading reservations: data is null."); }
					else if (!(reservationsSaveData is JArray reservationsJArray)) { throw new Exception($"Tried to load reservations, but data type is {reservationsSaveData.Type} instead of {JTokenType.Array}."); }
					else
					{
						var reservationManager = ReservationManager.Instance;
						reservationManager.LoadSaveData(reservationsJArray);
					}
				}

				// TODO: load more data?
			}
			catch (Exception e) { Main.OnCriticalFailure(e, "loading mod data"); }
		}
	}
}
