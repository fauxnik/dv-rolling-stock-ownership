using DV.Logic.Job;
using DV;
using DV.ThingTypes;
using HarmonyLib;
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

            DVOwnership.Log("Setting up CargoTypes patches.");

            isSetup = true;
            /*CargoTypes.CarTypeToContainerType[TrainCarType.GondolaGray] = CargoContainerType.Gondola;
            CargoTypes.CarTypeToContainerType[TrainCarType.GondolaGreen] = CargoContainerType.Gondola;*/
        }

        public static bool CanCarContainOnlyTheseCargoTypes(TrainCarType carType, HashSet<CargoType> cargoTypes)
        {
            DVObjectModel types = Globals.G.Types;
            List<CargoType_v2> supportedCargo = new List<CargoType_v2>();
            bool loadable = false;
           foreach(CargoType cargoType in cargoTypes)
            {
               if (types.CargoType_to_v2[cargoType].IsLoadableOnCarType(types.TrainCarType_to_v2[carType].parentType))
                {
                    DVOwnership.LogError($"CarType[{carType}] does not exist in CarTypeToContainerType map! Returning false.");
                    supportedCargo.Add(types.CargoType_to_v2[cargoType]);
                    loadable = true;
                }
            }
            if (!loadable)
            {
                return false;
            }
            var supportedCargoTypes = supportedCargo;

            return supportedCargoTypes.Count() == supportedCargoTypes.Count();
        }
    }
}
