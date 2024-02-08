using HarmonyLib;
using System.Collections.Generic;

namespace RollingStockOwnership.Patches;

public class UnusedTrainCarDeleter_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up unused train car deleter patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up UnusedTrainCarDeleter patches.");

		isSetup = true;
		var UnusedTrainCarDeleter_AreDeleteConditionsFulfilled = AccessTools.Method(typeof(UnusedTrainCarDeleter), "AreDeleteConditionsFulfilled");
		var UnusedTrainCarDeleter_AreDeleteConditionsFulfilled_Prefix = AccessTools.Method(typeof(UnusedTrainCarDeleter_Patches), "AreDeleteConditionsFulfilled_Prefix");
		Main.Patch(UnusedTrainCarDeleter_AreDeleteConditionsFulfilled, prefix: new HarmonyMethod(UnusedTrainCarDeleter_AreDeleteConditionsFulfilled_Prefix));
		var UnusedTrainCarDeleter_MarkForDelete = AccessTools.Method(typeof(UnusedTrainCarDeleter), "MarkForDelete", new System.Type[] { typeof(TrainCar) });
		var UnusedTrainCarDeleter_MarkForDelete_Prefix = AccessTools.Method(typeof(UnusedTrainCarDeleter_Patches), nameof(MarkForDelete_Prefix));
		Main.Patch(UnusedTrainCarDeleter_MarkForDelete, prefix: new HarmonyMethod(UnusedTrainCarDeleter_MarkForDelete_Prefix));
	}

	static bool AreDeleteConditionsFulfilled_Prefix(ref bool __result, TrainCar trainCar)
	{
		Main.LogVerbose(() => $"Checking delete conditions for train car with ID {trainCar.ID} and type {trainCar.carType}.");

		if (UnmanagedTrainCarTypes.UnmanagedTypes.Contains(trainCar.carType))
		{
			// Unmanaged train cars must use the vanilla logic
			return true;
		}

		__result = false;

		var equipment = RollingStockManager.Instance.FindByTrainCar(trainCar);
		if (equipment == null)
		{
			Main.LogError($"Checking delete conditions for a train car with ID {trainCar.ID}, which isn't recorded in the rolling stock registry! Returning true.");
			__result = true;
			return false;
		}

		__result = equipment.IsMarkedForDespawning;
		return false;
	}

	static bool MarkForDelete_Prefix(TrainCar unusedTrainCar, List<TrainCar> ___unusedTrainCarsMarkedForDelete)
	{
		if (___unusedTrainCarsMarkedForDelete.Contains(unusedTrainCar))
		{
			Main.LogWarning($"Attempting to mark {unusedTrainCar.ID} for deletion, but it's already marked for deletion.");
			return false;
		}
		return true;
	}
}
