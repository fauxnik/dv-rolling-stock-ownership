using DV;
using HarmonyLib;
using System.Collections.Generic;

namespace RollingStockOwnership.Patches;

public static class CommsRadioController_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up comms radio controller patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up CommsRadioController patches.");

		isSetup = true;
		var CommsRadioController_UpdateModesAvailability = AccessTools.Method(typeof(CommsRadioController), nameof(CommsRadioController.UpdateModesAvailability));
		var CommsRadioController_UpdateModesAvailability_Postfix = AccessTools.Method(typeof(CommsRadioController_Patches), "UpdateModesAvailability_Postfix");
		Main.Patch(CommsRadioController_UpdateModesAvailability, postfix: new HarmonyMethod(CommsRadioController_UpdateModesAvailability_Postfix));
	}

	static void UpdateModesAvailability_Postfix(CommsRadioController __instance, List<ICommsRadioMode> ___allModes, HashSet<int> ___disabledModeIndices, int ___activeModeIndex)
	{
		int spawnerIndex = ___allModes.IndexOf(__instance.carSpawnerControl);
		___disabledModeIndices.Add(spawnerIndex);
		if (___activeModeIndex == spawnerIndex)
		{
			AccessTools.Method(typeof(CommsRadioController), "SetNextMode").Invoke(__instance, null);
		}
	}
}
