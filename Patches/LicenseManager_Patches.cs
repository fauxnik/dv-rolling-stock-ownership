using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;
using DV.ThingTypes;
using DV.Utils;
using DV;

namespace DVOwnership.Patches
{
    public class LicenseManager_Patches
    {
        private static HashSet<CargoType>[] cargoTypesRequiringLicense = new HashSet<CargoType>[]
        {
            new HashSet<CargoType> { CargoType.CrudeOil },
            new HashSet<CargoType> {CargoType.Gasoline },
            new HashSet<CargoType> {CargoType.Diesel },
            new HashSet<CargoType> {CargoType.Methane },
            new HashSet<CargoType> {CargoType.ChemicalsIskar },
            new HashSet<CargoType> {CargoType.ChemicalsSperex },
        };

       /*private static HashSet<CargoType> containerTypesRequiringLicense = new HashSet<CargoType>
        {
            CargoType.Military1CarContainers,
        };*/

       /* public static bool IsLicensedForCargoTypes(CargoType cargoTypes)
        {

            LicenseManager licenseManager = SingletonBehaviour<LicenseManager>.Instance;
            return licenseManager.IsLicensedForJob(licenseManager.GetRequiredLicensesForCargoTypes(new HashSet<CargoType>cargoTypes));
        }*/

        public static bool IsLicensedForCargoType(HashSet<CargoType> containerTypes)
        {
            LicenseManager lm = SingletonBehaviour<LicenseManager>.Instance;
            return lm.IsLicensedForJob(lm.GetRequiredLicensesForCargoTypes(containerTypes));
        }

       public static bool IsLicensedForCar(TrainCarType carType)
        {
            DVObjectModel types = Globals.G.Types;
            var unlisencedCargoType = from containerTypes in cargoTypesRequiringLicense
                                           where !IsLicensedForCargoType(containerTypes)
                                           from containerType in containerTypes select containerType;
            var unlisencedCargoTypeV2 = convertToV2(unlisencedCargoType);
            foreach (CargoType_v2 cargo in unlisencedCargoTypeV2)
            {
               if( cargo.IsLoadableOnCarType(types.TrainCarType_to_v2[carType].parentType))
                {
                    return false;
                }
            }

           /* var unlicensedCargoTypes = from cargoTypes in cargoTypesRequiringLicense
                                       where !IsLicensedForCargoTypes(cargoTypes.ToList())
                                       from cargoType in cargoTypes
                                       select cargoType;*/

            /*if (CargoTypes_Patches.CanCarContainOnlyTheseCargoTypes(carType, unlisencedCargoType.ToHashSet()))
            {
                // Not licensed for cargo types
                return false;
            }*/

            return true;
        }
        private static HashSet<CargoType_v2> convertToV2(IEnumerable<CargoType> cargoType)
        {
            HashSet<CargoType_v2> cargoV2 = new HashSet<CargoType_v2>();
            DVObjectModel types = Globals.G.Types;
            foreach (CargoType cargo in cargoType)
            {
                cargoV2.Add(types.CargoType_to_v2[cargo]);
            }
            return cargoV2;
        }
       public static bool IsLicensedForLoco(TrainCarType carType)
        {
            DVObjectModel types = Globals.G.Types;
            LicenseManager lm = SingletonBehaviour<LicenseManager>.Instance;
            return CarTypes.IsTender(types.TrainCarType_to_v2[carType]) && lm.IsGeneralLicenseAcquired(types.GeneralLicenseType_to_v2[GeneralLicenseType.SH282]) || lm.IsLicensedForCar(types.TrainCarType_to_v2[carType]);
        }
    }
}
