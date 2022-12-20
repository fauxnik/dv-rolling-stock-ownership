using DV.Logic.Job;
using Harmony12;
using System.Linq;

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
        }

        static void GetYardTrackWithId_Postfix(string trackId, ref Track __result)
        {
            __result ??= SingletonBehaviour<CarsSaveManager>.Instance.OrderedRailtracks.Select(railTrack => railTrack.logicTrack).FirstOrDefault(logicTrack => logicTrack.ID.FullID == trackId);
        }
    }
}
