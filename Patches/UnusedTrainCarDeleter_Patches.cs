using Harmony12;

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

            isSetup = true;
            var UnusedTrainCarDeleter_AreDeleteConditionsFulfilled = AccessTools.Method(typeof(UnusedTrainCarDeleter), "AreDeleteConditionsFulfilled");
            var UnusedTrainCarDeleter_AreDeleteConditionsFulfilled_Prefix = AccessTools.Method(typeof(UnusedTrainCarDeleter_Patches), "AreDeleteConditionsFulfilled_Prefix");
            DVOwnership.Patch(UnusedTrainCarDeleter_AreDeleteConditionsFulfilled, prefix: new HarmonyMethod(UnusedTrainCarDeleter_AreDeleteConditionsFulfilled_Prefix));
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
    }
}
