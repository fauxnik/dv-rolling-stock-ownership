using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;
using System;
using DV.ThingTypes;
using DV.Utils;
using DV;
using DV.ThingTypes.TransitionHelpers;

namespace DVOwnership.Patches
{
    public class LicenseManager_Patches
    {
        private static HashSet<CargoType> cargoTypesRequiringLicense = new HashSet<CargoType>()
        {
            CargoType.Coal,
            CargoType.IronOre,
            CargoType.CrudeOil,
            CargoType.Diesel,
            CargoType.Gasoline,
            CargoType.Methane,
            CargoType.Logs,
            CargoType.Boards,
            CargoType.Plywood,
            CargoType.Wheat,
            CargoType.Corn,
            CargoType.Pigs,
            CargoType.Cows,
            CargoType.Chickens,
            CargoType.Sheep,
            CargoType.Goats,
            CargoType.Bread,
            CargoType.DairyProducts,
            CargoType.MeatProducts,
            CargoType.CannedFood,
            CargoType.CatFood,
            CargoType.SteelRolls,
            CargoType.SteelBillets,
            CargoType.SteelSlabs,
            CargoType.SteelBentPlates,
            CargoType.SteelRails,
            CargoType.ScrapMetal,
            CargoType.ElectronicsIskar,
            CargoType.ElectronicsKrugmann,
            CargoType.ElectronicsAAG,
            CargoType.ElectronicsNovae,
            CargoType.ElectronicsTraeg,
            CargoType.ToolsIskar,
            CargoType.ToolsBrohm,
            CargoType.ToolsAAG,
            CargoType.ToolsNovae,
            CargoType.ToolsTraeg,
            CargoType.Furniture,
            CargoType.Pipes,
            CargoType.ClothingObco,
            CargoType.ClothingNeoGamma,
            CargoType.ClothingNovae,
            CargoType.ClothingTraeg,
            CargoType.Medicine,
            CargoType.ChemicalsIskar,
            CargoType.ChemicalsSperex,
            CargoType.NewCars,
            CargoType.ImportedNewCars,
            CargoType.Tractors,
            CargoType.Excavators,
            CargoType.Alcohol,
            CargoType.Acetylene,
            CargoType.CryoOxygen,
            CargoType.CryoHydrogen,
            CargoType.Argon,
            CargoType.Nitrogen,
            CargoType.Ammonia,
            CargoType.SodiumHydroxide,
            CargoType.SpentNuclearFuel,
            CargoType.Ammunition,
            CargoType.Biohazard,
            CargoType.Tanks,
            CargoType.MilitaryTrucks,
            CargoType.MilitarySupplies,
            CargoType.EmptySunOmni,
            CargoType.EmptyIskar,
            CargoType.EmptyObco,
            CargoType.EmptyGoorsk,
            CargoType.EmptyKrugmann,
            CargoType.EmptyBrohm,
            CargoType.EmptyAAG,
            CargoType.EmptySperex,
            CargoType.EmptyNovae,
            CargoType.EmptyTraeg,
            CargoType.EmptyChemlek,
            CargoType.EmptyNeoGamma

        };

       /*private static HashSet<CargoType> containerTypesRequiringLicense = new HashSet<CargoType>
        {
            CargoType.Military1CarContainers,
        };*/

       public static bool IsLicensedForCargoTypes(HashSet<CargoType> cargoTypes)
        {

            LicenseManager licenseManager = SingletonBehaviour<LicenseManager>.Instance;
            return licenseManager.IsLicensedForJob(licenseManager.GetRequiredLicensesForCargoTypes(cargoTypes));
        }

        public static bool IsLicensedForCargoType(HashSet<CargoType> containerTypes)
        {
            LicenseManager lm = SingletonBehaviour<LicenseManager>.Instance;
            return lm.IsLicensedForJob(lm.GetRequiredLicensesForCargoTypes(containerTypes));
        }

       public static bool IsLicensedForCar(TrainCarType carType)
        {
           var unlisencedCargoType = from cargoType in cargoTypesRequiringLicense
                                      where !IsLicensedForCargoType(new HashSet<CargoType>() { cargoType })
                                       select cargoType;

            foreach (CargoType cargo in unlisencedCargoType)
            {
                if(!(carType.Equals(TrainCarType.LocoRailbus))){
                    if (TransitionHelpers.ToV2(cargo).IsLoadableOnCarType(TransitionHelpers.ToV2(carType).parentType))
                    {
                        return false;
                    }
                }
            }

            /*var unlicensedCargoTypes = from cargoTypes in cargoTypesRequiringLicense
                                        where !IsLicensedForCargoTypes(cargoTypes.ToList())
                                        from cargoType in cargoTypes
                                        select cargoType;*/
            if (!(carType.Equals(TrainCarType.LocoRailbus)))
            {
                if (CargoTypes_Patches.CanCarContainOnlyTheseCargoTypes(carType, unlisencedCargoType.ToHashSet()))
                {
                    // Not licensed for cargo types
                    return false;
                }
            }

            return true;
        }
        /*private static HashSet<CargoType_v2> convertToV2(IEnumerable<CargoType> cargoType)
        {
            HashSet<CargoType_v2> cargoV2 = new HashSet<CargoType_v2>();
            foreach (CargoType cargo in cargoType)
            {
                cargoV2.Add(TransitionHelpers.ToV2(cargo));
            }
            return cargoV2;
        }*/
       public static bool IsLicensedForLoco(TrainCarType carType)
       {
            LicenseManager lm = SingletonBehaviour<LicenseManager>.Instance;
            return lm.IsLicensedForCar(TransitionHelpers.ToV2(carType));
       }

    }
}
