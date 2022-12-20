using DV.Logic.Job;
using Harmony12;

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
            DVOwnership.Patch(JCCWEHG_OnLastJobInChainCompleted, prefix: new HarmonyMethod(JCCWEHG_OnLastJobInChainCompleted_Prefix));
        }

        // Instead of generating empty haul jobs, we need to generate the next job in the chain
        static bool OnLastJobInChainCompleted_Prefix(JobChainControllerWithEmptyHaulGeneration __instance, Job lastJobInChain)
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
            }

            return false;
        }
    }
}
