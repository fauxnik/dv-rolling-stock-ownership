using DV.Logic.Job;
using HarmonyLib;
using System.Collections.Generic;

namespace RollingStockOwnership.Patches;

public class IdGenerator_Patches
{
	private static bool isSetup = false;
	private static IdGenerator? idGenerator;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up ID generator patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up IdGenerator patches.");

		isSetup = true;
		idGenerator = IdGenerator.Instance;
		var IdGenerator_RegisterCarId = AccessTools.Method(typeof(IdGenerator), nameof(idGenerator.RegisterCarId));
		var IdGenerator_RegisterCarId_Prefix = AccessTools.Method(typeof(IdGenerator_Patches), nameof(RegisterCarId_Prefix));
		Main.Patch(IdGenerator_RegisterCarId, prefix: new HarmonyMethod(IdGenerator_RegisterCarId_Prefix));
		var IdGenerator_UnregisterCarId = AccessTools.Method(typeof(IdGenerator), nameof(idGenerator.UnregisterCarId));
		var IdGenerator_UnregisterCarId_Prefix = AccessTools.Method(typeof(IdGenerator_Patches), nameof(UnregisterCarId_Prefix));
		Main.Patch(IdGenerator_UnregisterCarId, prefix: new HarmonyMethod(IdGenerator_UnregisterCarId_Prefix));
	}

	// We will skip both vanilla methods entirely and use our own implementations instead
	static bool RegisterCarId_Prefix(string carId)
	{
		// TODO: how to get TrainCarLivery here so IDs of unmanaged car types can be registered?
		Main.LogDebug(() => "Skipping vanilla RegisterCarId.");
		return false;
	}
	static bool UnregisterCarId_Prefix(string carId)
	{
		// TODO: how to get TrainCarLivery here so IDs of unmanaged car types can be unregistered?
		Main.LogDebug(() => "Skipping vanilla UnregisterCarId.");
		return false;
	}

	public static void RegisterCarId(string carId)
	{
		Main.LogDebug(() => $"Registering car ID {carId}.");

		HashSet<string>? existingCarIds = AccessTools.Field(typeof(IdGenerator), "existingCarIds").GetValue(idGenerator) as HashSet<string>;
		if (existingCarIds == null)
		{
			Main.LogError("Couldn't retrieve existingCarIds field from IdGenerator!");
			return;
		}

		if (!existingCarIds.Add(carId))
		{
			Main.LogError($"carId: {carId} was already registered!");
		}
	}

	public static void UnregisterCarId(string carId)
	{
		Main.LogDebug(() => $"Unregistering car ID {carId}.");

		HashSet<string>? existingCarIds = AccessTools.Field(typeof(IdGenerator), "existingCarIds").GetValue(idGenerator) as HashSet<string>;
		if (existingCarIds == null)
		{
			Main.LogError("Couldn't retrieve existingCarIds field from IdGenerator!");
			return;
		}

		if (!existingCarIds.Remove(carId))
		{
			Main.LogError($"carId: {carId} wasn't registered!");
		}
	}
}
