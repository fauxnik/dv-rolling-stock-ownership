using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVOwnership.Patches
{
    public class StationProceduralJobsController_Patches
    {
        private static bool isSetup = false;
        private static Dictionary<StationController, StationProceduralJobsController> stationToVanillaJobsController = new Dictionary<StationController, StationProceduralJobsController>();
        private static Dictionary<StationController, ProceduralJobsController> stationToModJobsController = new Dictionary<StationController, ProceduralJobsController>();

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

        static bool TryToGenerateJobs_Prefix(StationProceduralJobsController __instance, ref Coroutine ___generationCoro, StationController ___stationController)
        {
            DVOwnership.Log($"Generating jobs for equipment at {__instance.stationController.logicStation.ID} station.");

            __instance.StopJobGeneration();
            ProceduralJobsController jobsController;
            if (!stationToModJobsController.TryGetValue(___stationController, out jobsController))
            {
                jobsController = new ProceduralJobsController(___stationController);
                stationToModJobsController.Add(___stationController, jobsController);
            }
            if (!stationToVanillaJobsController.ContainsKey(___stationController))
            {
                stationToVanillaJobsController.Add(___stationController, __instance);
            }
            
            ___generationCoro = __instance.StartCoroutine(jobsController.GenerateJobsCoro());

            DVOwnership.Log("Skipping default job generation.");
            return false;
        }

        public static void ReportJobGenerationComplete(StationController stationController)
        {
            StationProceduralJobsController proceduralJobsController;
            if (stationToVanillaJobsController.TryGetValue(stationController, out proceduralJobsController))
            {
                AccessTools.Field(typeof(StationProceduralJobsController), "generationCoro").SetValue(proceduralJobsController, null);
                return;
            }

            DVOwnership.LogError($"Couldn't find station procedural jobs controller for station controller {stationController.stationInfo.Name}!");
        }
    }
}
