using Harmony12;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVOwnership
{
    public class SpawnStateManager : SingletonBehaviour<SpawnStateManager>
    {
        public static new string AllowAutoCreate() { return "[DVOwnership_ProximityChecker]"; }

        private Coroutine proximityCoro;

        private static readonly float DELAY_SECONDS_BETWEEN_CHECK_CYCLES = 5f;
        private static readonly float SPAWN_SQR_DISTANCE = 360_000f;
        private static readonly float DESPAWN_SQR_DISTANCE = 490_000f;

        public void Start()
        {
            if (proximityCoro != null)
            {
                DVOwnership.LogWarning("Proximity checking coroutine start requested, but it was already running.");
                return;
            }

            DVOwnership.Log("Starting proximity checking coroutine.");
            proximityCoro = StartCoroutine(ProximityCheckCoro());
        }

        public void Stop()
        {
            if (proximityCoro == null)
            {
                DVOwnership.LogWarning("Proximity checking coroutine stop requested, but it wasn't running.");
                return;
            }

            DVOwnership.Log("Stopping proximity checking coroutine.");
            StopCoroutine(proximityCoro);
            proximityCoro = null;
        }

        private IEnumerator ProximityCheckCoro()
        {
            for (;;)
            {
                DVOwnership.LogDebug(() => "A");
                var rollingStock = SingletonBehaviour<RollingStockManager>.Instance;
                var seenGuids = new HashSet<string>();

                // TODO: coupled cars don't appear to be recorded as coupled. perhaps update all equipment here?

                foreach (var equipment in rollingStock.AllEquipment)
                {
                    DVOwnership.LogDebug(() => $"B\n\tequipment.ID: {equipment.ID}");
                    yield return null;
                    if (seenGuids.Contains(equipment.CarGUID)) { continue; }
                    var connectedEquipment = rollingStock.GetConnectedEquipment(equipment);

                    DVOwnership.LogDebug(() => $"C\n\tconnectedEquipment.Count: {connectedEquipment.Count}");
                    yield return null;
                    if (!connectedEquipment.All(eq => eq.IsSpawned == equipment.IsSpawned))
                    {
                        DVOwnership.LogError($"Connected equipment contains both spawned and despawned items! This should never happen.");
                    }

                    if (equipment.IsSpawned)
                    {
                        // Check if ALL connected equipment should be despawned
                        
                        var bestGuessLastDrivenTrainset = PlayerManager.LastLoco?.trainset;
                        
                        bool isDespawnable = !equipment.ExistsInTrainset(bestGuessLastDrivenTrainset);
                        foreach (var eq in connectedEquipment)
                        {
                            DVOwnership.LogDebug(() => $"D\n\tisDespawnable:{isDespawnable}\n\tIsStationary: {eq.IsStationary}\n\tSquaredDistanceFromPlayer: {eq.SquaredDistanceFromPlayer()}\n\tDESPAWN_SQR_DISTANCE: {DESPAWN_SQR_DISTANCE}\n\tflag: {isDespawnable && (!eq.IsStationary || eq.SquaredDistanceFromPlayer() < DESPAWN_SQR_DISTANCE)}");
                            yield return null;
                            seenGuids.Add(eq.CarGUID);
                            // Short circuit avoids doing expensive calculation unnecessarily
                            if (isDespawnable && (!eq.IsStationary || eq.SquaredDistanceFromPlayer() < DESPAWN_SQR_DISTANCE))
                            {
                                isDespawnable = false;
                                // Can't break here b/c we need to add all the guids to the hash set
                            }
                        }

                        if (isDespawnable)
                        {
                            DVOwnership.LogDebug(() => "E");
                            yield return null;
                            foreach (var eq in connectedEquipment) { eq.PrepareForDespawning(); }
                        }
                    }
                    else
                    {
                        // Check if ANY connected equipment should be spawned

                        bool isSpawnable = false;
                        foreach (var eq in connectedEquipment)
                        {
                            DVOwnership.LogDebug(() => $"F\n\tisSpawnable: {isSpawnable}\n\tIsStationary: {eq.IsStationary}\n\tSquaredDistanceFromPlayer: {eq.SquaredDistanceFromPlayer()}\n\tSPAWN_SQR_DISTANCE: {SPAWN_SQR_DISTANCE}\n\tflag: {!isSpawnable && eq.SquaredDistanceFromPlayer() < SPAWN_SQR_DISTANCE}");
                            yield return null;
                            seenGuids.Add(eq.CarGUID);
                            // Short circuit avoids doing expensive calculation unnecessarily
                            if (!isSpawnable && eq.SquaredDistanceFromPlayer() < SPAWN_SQR_DISTANCE)
                            {
                                isSpawnable = true;
                                // Can't break here b/c we need to add all the guids to the hash set
                            }
                        }

                        if (isSpawnable)
                        {
                            foreach(var eq in connectedEquipment)
                            {
                                DVOwnership.LogDebug(() => "G");
                                yield return null;
                                eq.Spawn();
                            }
                        }
                    }
                }

                DVOwnership.LogDebug(() => "H");
                yield return WaitFor.SecondsRealtime(DELAY_SECONDS_BETWEEN_CHECK_CYCLES);
            }
        }

        [HarmonyPatch(typeof(UnusedTrainCarDeleter), "OnEnable")]
        class UnusedTrainCarDeleter_OnEnable_Patch
        {
            static void Postfix()
            {
                DVOwnership.Log($"Starting {nameof(SpawnStateManager)} coroutine.");
                Instance.Start();
            }
        }

        [HarmonyPatch(typeof(UnusedTrainCarDeleter), "OnDisable")]
        class UnusedTrainCarDeleter_OnDisable_Patch
        {
            static void Postfix()
            {
                DVOwnership.Log($"Stopping {nameof(SpawnStateManager)} coroutine.");
                Instance.Stop();
            }
        }
    }
}
