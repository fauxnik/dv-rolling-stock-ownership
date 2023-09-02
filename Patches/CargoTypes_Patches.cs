using DV.Logic.Job;
using DV;
using DV.ThingTypes;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using DV.ThingTypes.TransitionHelpers;

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

        public static bool CanCarContainOnlyTheseCargoTypes(TrainCarType carType, HashSet<CargoType> cargoTypesNotLicensed,HashSet<CargoType> allCargo)
        {
            DVObjectModel types = Globals.G.Types;
            List<CargoType_v2> supportedCargo = new List<CargoType_v2>();
            List<CargoType_v2> unlicensedCargo= new List<CargoType_v2>();
            bool loadable = false;
           foreach(CargoType cargoType in allCargo)
           {
               if (TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(TransitionHelpers.ToV2(carType).parentType))
                {
                    DVOwnership.LogError($"CarType[{carType}] does not exist in CarTypeToContainerType map! Returning false.");
                    supportedCargo.Add(types.CargoType_to_v2[cargoType]);
                    loadable = true;
                }
            }
            foreach (CargoType cargoType in cargoTypesNotLicensed)
            {
                if (TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(TransitionHelpers.ToV2(carType).parentType))
                {
                    DVOwnership.LogError($"CarType[{carType}] does not exist in CarTypeToContainerType map! Returning false.");
                    unlicensedCargo.Add(types.CargoType_to_v2[cargoType]);
                }
            }
            if (!loadable)
            {
                return false;
            }
            return unlicensedCargo.Count() == supportedCargo.Count();
        }
    }
}
