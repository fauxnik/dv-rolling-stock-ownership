using DV;
using HarmonyLib;
using System;

namespace RollingStockOwnership.Patches;

public class CommsRadioCarDeleter_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up comms radio car deleter patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up CommsRadioCarDeleter patches.");

		isSetup = true;
		var CommsRadioCarDeleter_OnUse = AccessTools.Method(typeof(CommsRadioCarDeleter), "OnUse");
		var CommsRadioCarDeleter_OnUse_Prefix = AccessTools.Method(typeof(CommsRadioCarDeleter_Patches), nameof(OnUse_Prefix));
		Main.Patch(CommsRadioCarDeleter_OnUse, prefix: new HarmonyMethod(CommsRadioCarDeleter_OnUse_Prefix));
	}

	static void OnUse_Prefix(CommsRadioCarDeleter __instance, TrainCar ___carToDelete)
	{
		if (___carToDelete == null) { return; }

		// CommsRadioCarDeleter.State enum is private. This attempt to use Harmony/reflection to get the value didn't work (null reference exception).
		// object carDeleterState = AccessTools.Field(typeof(CommsRadioCarDeleter), "state").GetValue(__instance);
		// Type State = AccessTools.TypeByName("DV.CommsRadioCarDeleter+State");
		// object confirmDeleteEnumValue = State.GetField("ConfirmDelete").GetValue(State);
		// Main.LogDebug(() => $"[CarDeleterPatch] state: {carDeleterState}, ConfirmDelete enum value: {confirmDeleteEnumValue}");

		// CommsRadioCarDeleter.State enum is private. Casting it to an int allows for comparison.
		int carDeleterState = (int) AccessTools.Field(typeof(CommsRadioCarDeleter), "state").GetValue(__instance);
		int confirmDeleteEnumValue = 1; // Known from decompilation, but could change/break
		Main.LogDebug(() => $"[CarDeleterPatch] state: {carDeleterState}, ConfirmDelete enum value (hard coded): {confirmDeleteEnumValue}");

		if (carDeleterState == confirmDeleteEnumValue)
		{
			___carToDelete.OnDestroyCar += NewEquipmentRemoverLambda(___carToDelete);
		}
	}

	static Action<TrainCar> NewEquipmentRemoverLambda (TrainCar trainCar)
	{
		string id = trainCar.ID;
		string guid = trainCar.logicCar.carGuid;

		return (TrainCar _) => {
			Main.Log($"Train car {id} is being deleted. Attempting to remove it from the rolling stock registry.");

			var manager = RollingStockManager.Instance;
			var equipment = manager?.FindByCarGUID(guid);
			if (equipment == null)
			{
				Main.LogWarning($"Equipment record not found in the rolling stock registry.");
				return;
			}

			Main.Log($"Removing equipment with ID {equipment.ID} from the rolling stock registry.");

			manager?.Remove(equipment);
		};
	}
}
