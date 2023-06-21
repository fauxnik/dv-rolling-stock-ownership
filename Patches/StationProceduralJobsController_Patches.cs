using Harmony12;
using System;

namespace DVOwnership.Patches
{
	public class StationProceduralJobsController_Patches
	{
		private static bool isSetup = false;

		public static void Setup()
		{
			if (isSetup)
			{
				DVOwnership.LogWarning("Trying to set up station procedural jobs controller patches, but they've already been set up!");
				return;
			}

			DVOwnership.Log("Setting up StationProceduralJobsController patches.");

			isSetup = true;
			var StationProceduralJobsController_TryToGenerateJobs = AccessTools.Method(typeof(StationProceduralJobsController), nameof(StationProceduralJobsController.TryToGenerateJobs));
			var StationProceduralJobsController_TryToGenerateJobs_Prefix = AccessTools.Method(typeof(StationProceduralJobsController_Patches), nameof(TryToGenerateJobs_Prefix));
			DVOwnership.Patch(StationProceduralJobsController_TryToGenerateJobs, prefix: new HarmonyMethod(StationProceduralJobsController_TryToGenerateJobs_Prefix));
		}

		static bool TryToGenerateJobs_Prefix(StationProceduralJobsController __instance, StationController ___stationController)
		{
			DVOwnership.Log($"Generating jobs for equipment at {__instance.stationController.logicStation.ID} station.");

			__instance.StopJobGeneration();
			ProceduralJobsController jobsController = ProceduralJobsController.ForStation(___stationController);

			var generationCoroField = AccessTools.Field(typeof(StationProceduralJobsController), "generationCoro");
			Action onComplete = () => generationCoroField.SetValue(__instance, null);
			var generationCoro = __instance.StartCoroutine(jobsController.GenerateJobsCoro(onComplete));
			generationCoroField.SetValue(__instance, generationCoro);

			DVOwnership.Log("Skipping default job generation.");
			return false;
		}
	}
}
