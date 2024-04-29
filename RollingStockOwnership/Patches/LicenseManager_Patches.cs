using DV.ThingTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership.Patches;

public class LicenseManager_Patches
{
	public static bool IsLicensedForCargoTypes(List<CargoType> cargoTypes)
	{

		LicenseManager licenseManager = LicenseManager.Instance;
		return licenseManager.IsLicensedForJob(licenseManager.GetRequiredLicensesForCargoTypes(cargoTypes));
	}

	public static bool IsLicensedForCargoType(CargoType cargoType)
	{
		return IsLicensedForCargoTypes(new List<CargoType> { cargoType });
	}

	public static bool IsLicensedForCar(TrainCarLivery carLivery)
	{
		var unlicensedCargoTypes = from CargoType cargoType in Enum.GetValues(typeof(CargoType))
									where !IsLicensedForCargoType(cargoType)
									select cargoType;
		if (CargoTypes_Patches.CanCarContainOnlyTheseCargoTypes(carLivery, unlicensedCargoTypes.ToHashSet()))
		{
			// Not licensed for cargo types
			return false;
		}

		return true;
	}
}
