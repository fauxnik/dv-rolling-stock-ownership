﻿using DV;
using DV.ThingTypes;
using System.Collections.Generic;

namespace RollingStockOwnership.Extensions;

internal static class LicenseManager_Extensions
{
	public static bool IsLicensedForCargoTypes(this LicenseManager licenseManager, List<CargoType> cargoTypes)
	{
		return licenseManager.IsLicensedForJob(licenseManager.GetRequiredLicensesForCargoTypes(cargoTypes));
	}

	public static bool IsLicensedForCargoType(this LicenseManager licenseManager, CargoType cargoType)
	{
		return licenseManager.IsLicensedForCargoTypes(new List<CargoType> { cargoType });
	}

	public static bool IsLicensedForAnyCompatibleCargo(this LicenseManager licenseManager, TrainCarLivery carLivery)
	{
		foreach (CargoType_v2 cargoType in Globals.G.Types.CarTypeToLoadableCargo[carLivery.parentType])
		{
			if (licenseManager.IsLicensedForCargoType(cargoType.v1))
			{
				return true;
			}
		}

		return false;
	}
}
