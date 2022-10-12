using Harmony12;
using UnityEngine;

namespace DVOwnership.Patches
{
    internal class StationLocoSpawner_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up station loco spawner patches, but they've already been set up!");
                return;
            }

            isSetup = true;
            var StationLocoSpawner_Update = AccessTools.Method(typeof(StationLocoSpawner), "Update");
            var StationLocoSpawner_Update_Prefix = AccessTools.Method(typeof(StationLocoSpawner_Patches), nameof(Update_Prefix));
            DVOwnership.Patch(StationLocoSpawner_Update, prefix: new HarmonyMethod(StationLocoSpawner_Update_Prefix));
        }

        static bool Update_Prefix(StationLocoSpawner __instance, ref bool ___playerEnteredLocoSpawnRange, ref GameObject ___spawnTrackMiddleAnchor, ref float ___spawnLocoPlayerSqrDistanceFromTrack)
        {
            var playerTransform = PlayerManager.PlayerTransform;
            if (playerTransform == null || !SaveLoadController.carsAndJobsLoadingFinished)
            {
                return true;
            }
            bool isPlayerInRange = (playerTransform.position - ___spawnTrackMiddleAnchor.transform.position).sqrMagnitude < ___spawnLocoPlayerSqrDistanceFromTrack;
            if (!___playerEnteredLocoSpawnRange && isPlayerInRange)
            {
                ___playerEnteredLocoSpawnRange = true;
                // We don't want any locomotives spawning automatically, so we'll skip the Update method entirely.
                DVOwnership.Log($"Skipping locomotive spawning for {__instance.locoSpawnTrackName}.");
                return false;
            }
            return true;
        }
    }
}
