using DV.Logic.Job;
using HarmonyLib;
using System.Collections.Generic;

namespace RollingStockOwnership.Patches;

public class StationProceduralJobsController_Patches
{
	private static bool isSetup = false;
	private static Dictionary<StationController, StationProceduralJobsController> instances = new Dictionary<StationController, StationProceduralJobsController>();

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up station procedural jobs controller patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up StationProceduralJobsController patches.");

		isSetup = true;
		var StationProceduralJobsController_Awake = AccessTools.Method(typeof(StationProceduralJobsController), "Awake");
		var StationProceduralJobsController_Awake_Postfix = AccessTools.Method(typeof(StationProceduralJobsController_Patches), nameof(Awake_Postfix));
		Main.Patch(StationProceduralJobsController_Awake, postfix: new HarmonyMethod(StationProceduralJobsController_Awake_Postfix));
		var StationProceduralJobsController_TryToGenerateJobs = AccessTools.Method(typeof(StationProceduralJobsController), nameof(StationProceduralJobsController.TryToGenerateJobs));
		var StationProceduralJobsController_TryToGenerateJobs_Prefix = AccessTools.Method(typeof(StationProceduralJobsController_Patches), nameof(TryToGenerateJobs_Prefix));
		Main.Patch(StationProceduralJobsController_TryToGenerateJobs, prefix: new HarmonyMethod(StationProceduralJobsController_TryToGenerateJobs_Prefix));
	}

	static void Awake_Postfix(StationProceduralJobsController __instance)
	{
		instances.Add(__instance.stationController, __instance);
	}

	static bool TryToGenerateJobs_Prefix(StationProceduralJobsController __instance, StationController ___stationController)
	{
		Main.Log($"Generating jobs for equipment at {__instance.stationController.logicStation.ID} station.");

		StartJobGenerationCoroutine(__instance, ___stationController);

		Main.Log("Skipping default job generation.");
		return false;
	}

	public static StationProceduralJobsController? JobsControllerForStation(StationController stationController)
	{
		if (instances.TryGetValue(stationController, out StationProceduralJobsController jobsController))
		{
			return jobsController;
		}

		return null;
	}

	public static bool StartJobGenerationCoroutine(StationController stationController, IEnumerable<Car> carsToUse)
	{
		var jobsController = JobsControllerForStation(stationController);
		if (jobsController == null) { return false; }

		StartJobGenerationCoroutine(jobsController, stationController, carsToUse);
		return true;
	}

	private static void StartJobGenerationCoroutine(StationProceduralJobsController instance, StationController stationController, IEnumerable<Car>? carsToUse = null)
	{
		instance.StopJobGeneration();
		ProceduralJobsController jobsController = ProceduralJobsController.ForStation(stationController);

		var generationCoroField = AccessTools.Field(typeof(StationProceduralJobsController), "generationCoro");
		void onComplete() => generationCoroField.SetValue(instance, null);
		var generationCoro = instance.StartCoroutine(jobsController.GenerateJobsCoro(onComplete, carsToUse));
		generationCoroField.SetValue(instance, generationCoro);
	}
}
