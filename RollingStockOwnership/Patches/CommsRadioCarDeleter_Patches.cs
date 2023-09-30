using DV;
using HarmonyLib;

namespace RollingStockOwnership.Patches;

public class CommsRadioCarDeleter_Patches
{
	private static bool isSetup = false;
	private static string? carToMaybeDeleteGuid;

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
		var CommsRadioCarDeleter_OnCarToDeleteDestroy = AccessTools.Method(typeof(CommsRadioCarDeleter), "OnCarToDeleteDestroy");
		var CommsRadioCarDeleter_OnCarToDeleteDestroy_Postfix = AccessTools.Method(typeof(CommsRadioCarDeleter_Patches), nameof(OnCarToDeleteDestroy_Postfix));
		Main.Patch(CommsRadioCarDeleter_OnCarToDeleteDestroy, postfix: new HarmonyMethod(CommsRadioCarDeleter_OnCarToDeleteDestroy_Postfix));
	}

	static void OnUse_Prefix(TrainCar ___carToDelete)
	{
		carToMaybeDeleteGuid = ___carToDelete?.CarGUID;
	}

	static void OnCarToDeleteDestroy_Postfix()
	{
		if (string.IsNullOrEmpty(carToMaybeDeleteGuid))
		{
			Main.LogError("Car GUID is missing while attempting to remove a deleted train car's entry from the rolling stock registry!");
			return;
		}

		Main.Log($"Train car is being deleted. Attempting to remove it from the rolling stock registry.");

		var manager = RollingStockManager.Instance;
		var equipment = manager?.FindByCarGUID(carToMaybeDeleteGuid);
		if (equipment == null)
		{
			Main.LogWarning($"Equipment record not found in the rolling stock registry.");
			return;
		}

		Main.Log($"Removing equipment with ID {equipment.ID} from the rolling stock registry.");

		manager?.Remove(equipment);
		carToMaybeDeleteGuid = null;
	}
}
