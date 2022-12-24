using Harmony12;
using System.Collections.Generic;

namespace DVOwnership.Patches
{
    public class UnusedTrainCarDeleter_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up unused train car deleter patches, but they've already been set up!");
                return;
            }

            DVOwnership.Log("Setting up UnusedTrainCarDeleter patches.");

            isSetup = true;
            var UnusedTrainCarDeleter_AreDeleteConditionsFulfilled = AccessTools.Method(typeof(UnusedTrainCarDeleter), "AreDeleteConditionsFulfilled");
            var UnusedTrainCarDeleter_AreDeleteConditionsFulfilled_Prefix = AccessTools.Method(typeof(UnusedTrainCarDeleter_Patches), "AreDeleteConditionsFulfilled_Prefix");
            DVOwnership.Patch(UnusedTrainCarDeleter_AreDeleteConditionsFulfilled, prefix: new HarmonyMethod(UnusedTrainCarDeleter_AreDeleteConditionsFulfilled_Prefix));
            var UnusedTrainCarDeleter_MarkForDelete = AccessTools.Method(typeof(UnusedTrainCarDeleter), "MarkForDelete", new System.Type[] { typeof(TrainCar) });
            var UnusedTrainCarDeleter_MarkForDelete_Prefix = AccessTools.Method(typeof(UnusedTrainCarDeleter_Patches), nameof(MarkForDelete_Prefix));
            DVOwnership.Patch(UnusedTrainCarDeleter_MarkForDelete, prefix: new HarmonyMethod(UnusedTrainCarDeleter_MarkForDelete_Prefix));
        }

        static bool AreDeleteConditionsFulfilled_Prefix(ref bool __result, TrainCar trainCar)
        {
            __result = false;

            var equipment = SingletonBehaviour<RollingStockManager>.Instance.FindByTrainCar(trainCar);
            if (equipment == null)
            {
                DVOwnership.LogError($"Checking delete conditions for a train car with ID {trainCar.ID}, which isn't recorded in the rolling stock registry! Returning true.");
                __result = true;
                return false;
            }

            __result = equipment.IsMarkedForDespawning;
            return false;
        }

        static bool MarkForDelete_Prefix(TrainCar unusedTrainCar, List<TrainCar> ___unusedTrainCarsMarkedForDelete)
        {
            if (___unusedTrainCarsMarkedForDelete.Contains(unusedTrainCar))
            {
                DVOwnership.LogWarning($"Attempting to mark {unusedTrainCar.ID} for deletion, but it's already marked for deletion.");
                return false;
            }
            return true;
        }
    }
}
