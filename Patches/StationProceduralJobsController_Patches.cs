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

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up station procedural jobs controller patches, but they've already been set up!");
                return;
            }

            isSetup = true;
            var StationProceduralJobsController_TryToGenerateJobs = AccessTools.Method(typeof(StationProceduralJobsController), nameof(StationProceduralJobsController.TryToGenerateJobs));
            var StationProceduralJobsController_TryToGenerateJobs_Prefix = AccessTools.Method(typeof(StationProceduralJobsController_Patches), nameof(TryToGenerateJobs_Prefix));
            DVOwnership.Patch(StationProceduralJobsController_TryToGenerateJobs, prefix: new HarmonyMethod(StationProceduralJobsController_TryToGenerateJobs_Prefix));
        }

        static bool TryToGenerateJobs_Prefix(StationProceduralJobsController __instance, ref Coroutine ___generationCoro)
        {
            DVOwnership.Log($"Generating jobs for equipment at {__instance.stationController.logicStation.ID} station.");
            __instance.StopJobGeneration();
            // TODO: start the job generation coroutine and set it below
            ___generationCoro = null;

            DVOwnership.Log("Skipping default job generation.");
            return false;
        }
    }
}
