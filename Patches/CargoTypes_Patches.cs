using DV.Logic.Job;
using Harmony12;
using System.Collections.Generic;
using System.Linq;

namespace DVOwnership.Patches
{
    public class CargoTypes_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to setup cargo types patches, but they've already been set up!");
                return;
            }

            isSetup = true;
            CargoTypes.CarTypeToContainerType[TrainCarType.GondolaGray] = CargoContainerType.Gondola;
            CargoTypes.CarTypeToContainerType[TrainCarType.GondolaGreen] = CargoContainerType.Gondola;
        }

        public static bool CanCarContainOnlyTheseCargoTypes(TrainCarType carType, HashSet<CargoType> cargoTypes)
        {
            CargoContainerType containerType;
            if (!CargoTypes.CarTypeToContainerType.TryGetValue(carType, out containerType))
            {
                DVOwnership.LogError($"CarType[{carType}] does not exist in CarTypeToContainerType map! Returning false.");
                return false;
            }

            var cargoTypeToSupportedCarContainer = AccessTools.Field(typeof(CargoTypes), "cargoTypeToSupportedCarContainer").GetValue(null) as Dictionary<CargoType, List<CargoContainerType>>;
            if (cargoTypeToSupportedCarContainer == null)
            {
                DVOwnership.LogError($"Couldn't retrieve cargoTypeToSupportedCarContainer from CargoTypes! Returning false.");
                return false;
            }
            var supportedCargoTypes = from kvPair in cargoTypeToSupportedCarContainer
                                      where kvPair.Value.Contains(containerType)
                                      select kvPair.Key;

            return supportedCargoTypes.Count() == supportedCargoTypes.Intersect(cargoTypes).Count();
        }
    }
}
