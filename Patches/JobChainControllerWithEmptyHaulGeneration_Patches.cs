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

        // We don't want any empty haul jobs to be generated EVER!
        static bool OnLastJobInChainCompleted_Prefix()
        {
            return false;
        }
    }
}
