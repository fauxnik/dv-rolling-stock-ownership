using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;

namespace DVOwnership.Patches
{
	public class LicenseManager_Patches
	{
		private static HashSet<CargoType>[] cargoTypesRequiringLicense = new HashSet<CargoType>[]
		{
			CargoTypes.Hazmat1Cargo,
			CargoTypes.Hazmat2Cargo,
			CargoTypes.Hazmat3Cargo,
			CargoTypes.Military2Cargo,
			CargoTypes.Military3Cargo,
		};

		private static HashSet<CargoContainerType>[] containerTypesRequiringLicense = new HashSet<CargoContainerType>[]
		{
			CargoTypes.Military1CarContainers,
		};

		public static bool IsLicensedForCargoTypes(List<CargoType> cargoTypes)
		{
			return LicenseManager.IsLicensedForJob(LicenseManager.GetRequiredLicensesForCargoTypes(cargoTypes));
		}

		public static bool IsLicensedForContainerTypes(HashSet<CargoContainerType> containerTypes)
		{
			return LicenseManager.IsLicensedForJob(LicenseManager.GetRequiredLicensesForCarContainerTypes(containerTypes));
		}

		public static bool IsLicensedForCar(TrainCarType carType)
		{
			var unlicensedContainerTypes = from containerTypes in containerTypesRequiringLicense
										   where !IsLicensedForContainerTypes(containerTypes)
										   from containerType in containerTypes select containerType;

			if (unlicensedContainerTypes.Contains(CargoTypes.CarTypeToContainerType[carType]))
			{
				// Not licensed for container type
				return false;
			}

			var unlicensedCargoTypes = from cargoTypes in cargoTypesRequiringLicense
									   where !IsLicensedForCargoTypes(cargoTypes.ToList())
									   from cargoType in cargoTypes
									   select cargoType;

			if (CargoTypes_Patches.CanCarContainOnlyTheseCargoTypes(carType, unlicensedCargoTypes.ToHashSet()))
			{
				// Not licensed for cargo types
				return false;
			}

			return true;
		}

		public static bool IsLicensedForLoco(TrainCarType carType)
		{
			return CarTypes.IsTender(carType) && LicenseManager.IsGeneralLicenseAcquired(GeneralLicenseType.SH282) || LicenseManager.IsLicensedForCar(carType);
		}
	}
}
