using DV.Logic.Job;
using DV.Utils;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;

namespace DVOwnership.Patches
{
    public class Track_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up track patches, but they've already been set up!");
                return;
            }

            DVOwnership.Log("Setting up Track patches.");
            isSetup = true;
            var Track_OccupiedLength_get = AccessTools.Method(typeof(Track), "get_OccupiedLength");
            var Track_OccupiedLength_get_Postfix = AccessTools.Method(typeof(Track_Patches), nameof(OccupiedLength_get_Postfix));
            DVOwnership.Patch(Track_OccupiedLength_get, postfix: new HarmonyMethod(Track_OccupiedLength_get_Postfix));
            var Track_IsFree = AccessTools.Method(typeof(Track), nameof(Track.IsFree));
            var Track_IsFree_Postfix = AccessTools.Method(typeof(Track_Patches), nameof(IsFree_Postfix));
            DVOwnership.Patch(Track_IsFree, postfix: new HarmonyMethod(Track_IsFree_Postfix));
        }

        static void OccupiedLength_get_Postfix(Track __instance, ref float __result)
        {
            var yto = SingletonBehaviour<YardTracksOrganizer>.Instance;
            var carSpawner = SingletonBehaviour<CarSpawner>.Instance;   
            var rsm = SingletonBehaviour<RollingStockManager>.Instance;
            var equipmentOnTrack = rsm.GetEquipmentOnTrack(__instance);
            List<Car> cars = new List<Car>();
            foreach(Equipment equipment in equipmentOnTrack)
            {
                cars.Add(equipment.GetLogicCar());
            }
            var lengthOfEquipment = carSpawner.GetTotalCarsLength(cars);
            var occupiedLength = lengthOfEquipment + carSpawner.GetSeparationLengthBetweenCars(equipmentOnTrack.Count());
            DVOwnership.LogDebug(() => $"[OccupiedLength] Track: {__instance.ID.FullDisplayID}\n\tspawned: {equipmentOnTrack.Where(eq => eq.IsSpawned).Count()} cars\n\tunspawned: {equipmentOnTrack.Where(eq => !eq.IsSpawned).Count()} cars\n\toccupied: {occupiedLength}m");
            __result = occupiedLength;
        }

        static void IsFree_Postfix(Track __instance, ref bool __result)
        {
            var rsm = SingletonBehaviour<RollingStockManager>.Instance;
            var equipmentOnTrack = rsm.GetEquipmentOnTrack(__instance);
            var isFree = equipmentOnTrack.Count() == 0;
            DVOwnership.LogDebug(() => $"[IsFree()] Track: {__instance.ID.FullDisplayID}\n\tspawned: {equipmentOnTrack.Where(eq => eq.IsSpawned).Count()} cars\n\tunspawned: {equipmentOnTrack.Where(eq => !eq.IsSpawned).Count()} cars\n\tfree?: {(isFree ? "yes" : "no")}");
            __result = isFree;
        }
    }
}
