using DV.Logic.Job;
using DV.Utils;
using DV.ThingTypes;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace DVOwnership.Patches
{
	public class JobChainControllerWithEmptyHaulGeneration_Patches
	{
		private static bool isSetup = false;

		public static void Setup()
		{
			if (isSetup)
			{
				DVOwnership.LogWarning("Trying to set up JobChainControllerWithEmptyHaulGeneration patches, but they've already been set up!");
				return;
			}

			DVOwnership.Log("Setting up JobChainControllerWithEmptyHaulGeneration patches.");

			isSetup = true;
			var JCCWEHG_OnLastJobInChainCompleted = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration), "OnLastJobInChainCompleted");
			var JCCWEHG_OnLastJobInChainCompleted_Prefix = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration_Patches), nameof(OnLastJobInChainCompleted_Prefix));
			var JCWEHG_OnLastJobInChainCompleted_Transpiler = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration_Patches), nameof(OnLastJobInChainCompleted_Transpiler));
			DVOwnership.Patch(JCCWEHG_OnLastJobInChainCompleted, prefix: new HarmonyMethod(JCCWEHG_OnLastJobInChainCompleted_Prefix), transpiler: new HarmonyMethod(JCWEHG_OnLastJobInChainCompleted_Transpiler));
		}

		// Instead of generating empty haul jobs, we need to generate the next job in the chain.
		static void OnLastJobInChainCompleted_Prefix(JobChainControllerWithEmptyHaulGeneration __instance, Job lastJobInChain)
		{
			var jobType = lastJobInChain.jobType;
			var logicController = SingletonBehaviour<LogicController>.Instance;
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
				ProceduralJobGenerators.SetDestination(__instance, destinationController.logicStation.ID);
			}
			else if (jobType == JobType.ShuntingUnload)
			{
				ProceduralJobGenerators.SetDestination(__instance, null);
				if (!StationProceduralJobsController_Patches.StartJobGenerationCoroutine(destinationController, __instance.trainCarsForJobChain.Select(trainCar => trainCar.logicCar)))
				{
					DVOwnership.LogWarning($"Couldn't start job generation coroutine for ${destinationController.logicStation.ID}.\nGeneration of a new shunting load job for cars from ${lastJobInChain.ID} hasn't been attempted.");
				}
			}
		}

		// We don't want any empty haul jobs generated; we just need the base method implementation to be called.
		static IEnumerable<CodeInstruction> OnLastJobInChainCompleted_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			DVOwnership.LogDebug(() => "[START] JobChainControllerWithEmptyHaulGeneration transpiler");
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Ldarg_1);
			var callInstruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JobChainController), "OnLastJobInChainCompleted"));
			DVOwnership.LogDebug(() => callInstruction.ToString());
			yield return callInstruction;
			yield return new CodeInstruction(OpCodes.Ret);
			DVOwnership.LogDebug(() => "[END] JobChainControllerWithEmptyHaulGeneration transpiler");
		}
	}
}
