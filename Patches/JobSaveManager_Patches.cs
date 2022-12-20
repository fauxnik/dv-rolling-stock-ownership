using DV.Logic.Job;
using Harmony12;
using System.Linq;
using UnityEngine;

namespace DVOwnership.Patches
{
    public class JobSaveManager_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up JobSaveManager patches, but they've already been set up!");
                return;
            }

            DVOwnership.Log("Setting up JobSaveManager patches.");

            isSetup = true;
            var JobSaveManager_GetYardTrackWithId = AccessTools.Method(typeof(JobSaveManager), "GetYardTrackWithId");
            var JobSaveManager_GetYardTrackWithId_Postfix = AccessTools.Method(typeof(JobSaveManager_Patches), nameof(GetYardTrackWithId_Postfix));
            DVOwnership.Patch(JobSaveManager_GetYardTrackWithId, postfix: new HarmonyMethod(JobSaveManager_GetYardTrackWithId_Postfix));
            var JobSaveManager_LoadJobChain = AccessTools.Method(typeof(JobSaveManager), nameof(JobSaveManager.LoadJobChain));
            var JobSaveManager_LoadJobChain_Postfix = AccessTools.Method(typeof(JobSaveManager_Patches), nameof(LoadJobChain_Postfix));
            DVOwnership.Patch(JobSaveManager_LoadJobChain, postfix: new HarmonyMethod(JobSaveManager_LoadJobChain_Postfix));
        }

        static void GetYardTrackWithId_Postfix(string trackId, ref Track __result)
        {
            __result ??= SingletonBehaviour<CarsSaveManager>.Instance.OrderedRailtracks.Select(railTrack => railTrack.logicTrack).FirstOrDefault(logicTrack => logicTrack.ID.FullID == trackId);
        }

        static void LoadJobChain_Postfix(JobChainSaveData chainSaveData, ref GameObject __result)
        {
            var jobChainController = __result.GetComponent<JobChainController>() ?? __result.GetComponent<JobChainControllerWithEmptyHaulGeneration>();
            var currentJob = jobChainController.currentJobInChain;
            var logicController = SingletonBehaviour<LogicController>.Instance;
            var yardToStation = logicController.YardIdToStationController;
            var originController = yardToStation[currentJob.chainData.chainOriginYardId];
            var destinationController = yardToStation[currentJob.chainData.chainDestinationYardId];
            if (currentJob.jobType == JobType.ShuntingLoad)
            {
                jobChainController.JobChainCompleted += ProceduralJobGenerators.GenerateContinuationTransportJob(originController, destinationController);
            }
            else if (currentJob.jobType == JobType.Transport)
            {
                jobChainController.JobChainCompleted += ProceduralJobGenerators.GenerateContinuationUnloadJob(originController, destinationController);
            }
        }
    }
}
