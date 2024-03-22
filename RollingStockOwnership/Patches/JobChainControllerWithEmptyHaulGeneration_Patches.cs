﻿using DV.Logic.Job;
using DV.ThingTypes;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace RollingStockOwnership.Patches;

public class JobChainControllerWithEmptyHaulGeneration_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up JobChainControllerWithEmptyHaulGeneration patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up JobChainControllerWithEmptyHaulGeneration patches.");

		isSetup = true;
		var JCCWEHG_OnLastJobInChainCompleted = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration), "OnLastJobInChainCompleted");
		var JCCWEHG_OnLastJobInChainCompleted_Prefix = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration_Patches), nameof(OnLastJobInChainCompleted_Prefix));
		var JCWEHG_OnLastJobInChainCompleted_Transpiler = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration_Patches), nameof(OnLastJobInChainCompleted_Transpiler));
		Main.Patch(JCCWEHG_OnLastJobInChainCompleted, prefix: new HarmonyMethod(JCCWEHG_OnLastJobInChainCompleted_Prefix), transpiler: new HarmonyMethod(JCWEHG_OnLastJobInChainCompleted_Transpiler));
	}

	// Instead of generating empty haul jobs, we need to generate the next job in the chain.
	static void OnLastJobInChainCompleted_Prefix(JobChainControllerWithEmptyHaulGeneration __instance, Job lastJobInChain)
	{
		var jobType = lastJobInChain.jobType;
		var logicController = LogicController.Instance;
		var yardIdToStationController = logicController.YardIdToStationController;
		var originController = yardIdToStationController[lastJobInChain.chainData.chainOriginYardId];
		var destinationController = yardIdToStationController[lastJobInChain.chainData.chainDestinationYardId];

		if (jobType == JobType.ShuntingLoad)
		{
			ProceduralJobGenerators.GenerateContinuationTransportJob(__instance, originController, destinationController);
		}
		else if (jobType == JobType.Transport)
		{
			ProceduralJobGenerators.GenerateContinuationUnloadJob(__instance, originController, destinationController);
		}
		else if (jobType == JobType.ShuntingUnload)
		{
			if (!StationProceduralJobsController_Patches.StartJobGenerationCoroutine(destinationController, __instance.trainCarsForJobChain.Select(trainCar => trainCar.logicCar)))
			{
				Main.LogWarning($"Couldn't start job generation coroutine for ${destinationController.logicStation.ID}.\nGeneration of a new shunting load job for cars from ${lastJobInChain.ID} hasn't been attempted.");
			}
		}
	}

	// We don't want any empty haul jobs generated; we just need the base method implementation to be called.
	static IEnumerable<CodeInstruction> OnLastJobInChainCompleted_Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		Main.LogDebug(() => "[START] JobChainControllerWithEmptyHaulGeneration transpiler");
		yield return new CodeInstruction(OpCodes.Ldarg_0);
		yield return new CodeInstruction(OpCodes.Ldarg_1);
		var callInstruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JobChainController), "OnLastJobInChainCompleted"));
		Main.LogDebug(() => callInstruction.ToString());
		yield return callInstruction;
		yield return new CodeInstruction(OpCodes.Ret);
		Main.LogDebug(() => "[END] JobChainControllerWithEmptyHaulGeneration transpiler");
	}
}
