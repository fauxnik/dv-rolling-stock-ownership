using DV.Logic.Job;
using Harmony12;
using System.Collections.Generic;

namespace DVOwnership.Patches
{
	public class IdGenerator_Patches
	{
		private static bool isSetup = false;
		private static IdGenerator idGenerator;

		public static void Setup()
		{
			if (isSetup)
			{
				DVOwnership.LogWarning("Trying to set up ID generator patches, but they've already been set up!");
				return;
			}

			DVOwnership.Log("Setting up IdGenerator patches.");

			isSetup = true;
			idGenerator = SingletonBehaviour<IdGenerator>.Instance;
			var IdGenerator_RegisterCarId = AccessTools.Method(typeof(IdGenerator), nameof(idGenerator.RegisterCarId));
			var IdGenerator_RegisterCarId_Prefix = AccessTools.Method(typeof(IdGenerator_Patches), nameof(RegisterCarId_Prefix));
			DVOwnership.Patch(IdGenerator_RegisterCarId, prefix: new HarmonyMethod(IdGenerator_RegisterCarId_Prefix));
			var IdGenerator_UnregisterCarId = AccessTools.Method(typeof(IdGenerator), nameof(idGenerator.UnregisterCarId));
			var IdGenerator_UnregisterCarId_Prefix = AccessTools.Method(typeof(IdGenerator_Patches), nameof(UnregisterCarId_Prefix));
			DVOwnership.Patch(IdGenerator_UnregisterCarId, prefix: new HarmonyMethod(IdGenerator_UnregisterCarId_Prefix));
		}

		// We will skip both vanilla methods entirely and use our own implementations instead
		static bool RegisterCarId_Prefix(string carId)
		{
			// TODO: how to get TrainCarType here so IDs of unmanaged car types can be registered?
			DVOwnership.LogDebug(() => "Skipping vanilla RegisterCarId.");
			return false;
		}
		static bool UnregisterCarId_Prefix(string carId)
		{
			// TODO: how to get TrainCarType here so IDs of unmanaged car types can be unregistered?
			DVOwnership.LogDebug(() => "Skipping vanilla UnregisterCarId.");
			return false;
		}

		public static void RegisterCarId(string carId)
		{
			DVOwnership.LogDebug(() => $"Registering car ID {carId}.");

			HashSet<string> existingCarIds = AccessTools.Field(typeof(IdGenerator), "existingCarIds").GetValue(idGenerator) as HashSet<string>;
			if (existingCarIds == null)
			{
				DVOwnership.LogError("Couldn't retrieve existingCarIds field from IdGenerator!");
				return;
			}

			if (!existingCarIds.Add(carId))
			{
				DVOwnership.LogError($"carId: {carId} was already registered!");
			}
		}

		public static void UnregisterCarId(string carId)
		{
			DVOwnership.LogDebug(() => $"Unregistering car ID {carId}.");

			HashSet<string> existingCarIds = AccessTools.Field(typeof(IdGenerator), "existingCarIds").GetValue(idGenerator) as HashSet<string>;
			if (existingCarIds == null)
			{
				DVOwnership.LogError("Couldn't retrieve existingCarIds field from IdGenerator!");
				return;
			}

			if (!existingCarIds.Remove(carId))
			{
				DVOwnership.LogError($"carId: {carId} wasn't registered!");
			}
		}
	}
}
