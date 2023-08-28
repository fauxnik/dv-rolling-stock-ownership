using DV.Utils;
using HarmonyLib;

namespace DVOwnership.Patches
{
    public class TrainCar_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up train car patches, but they've already been set up!");
                return;
            }

            DVOwnership.Log("Setting up TrainCar patches.");

            isSetup = true;
            var TrainCar_PrepareForDestroy = AccessTools.Method(typeof(TrainCar), nameof(TrainCar.PrepareForDestroy));
            var TrainCar_PrepareForDestroy_Prefix = AccessTools.Method(typeof(TrainCar_Patches), nameof(PrepareForDestroy_Prefix));
            DVOwnership.Patch(TrainCar_PrepareForDestroy, prefix: new HarmonyMethod(TrainCar_PrepareForDestroy_Prefix));
        }

        static void PrepareForDestroy_Prefix(TrainCar __instance)
        {
            var equipment = SingletonBehaviour<RollingStockManager>.Instance.FindByTrainCar(__instance);
            if (equipment == null)
            {
                DVOwnership.LogError($"Preparing train car with ID {__instance.ID} for despawning, but it isn't recorded in the rolling stock registry!");
                return;
            }

            DVOwnership.Log($"Updating equipment record with ID {equipment.ID} because its train car is being despawned.");
            equipment.Update(__instance, true);
        }
    }
}
