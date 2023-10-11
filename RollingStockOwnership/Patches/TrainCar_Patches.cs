using HarmonyLib;

namespace RollingStockOwnership.Patches;

public class TrainCar_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up train car patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up TrainCar patches.");

		isSetup = true;
		var TrainCar_PrepareForDestroy = AccessTools.Method(typeof(TrainCar), nameof(TrainCar.PrepareForDestroy));
		var TrainCar_PrepareForDestroy_Prefix = AccessTools.Method(typeof(TrainCar_Patches), nameof(PrepareForDestroy_Prefix));
		Main.Patch(TrainCar_PrepareForDestroy, prefix: new HarmonyMethod(TrainCar_PrepareForDestroy_Prefix));
	}

	static void PrepareForDestroy_Prefix(TrainCar __instance)
	{
		var equipment = RollingStockManager.Instance.FindByTrainCar(__instance);
		if (equipment == null)
		{
			Main.LogError($"Preparing train car with ID {__instance.ID} for despawning, but it isn't recorded in the rolling stock registry!");
			return;
		}

		Main.Log($"Updating equipment record with ID {equipment.ID} because its train car is being despawned.");
		equipment.Update(__instance, true);
	}
}
