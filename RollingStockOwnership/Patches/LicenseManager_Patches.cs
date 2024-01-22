using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership.Patches;

public class LicenseManager_Patches
{
	private static HashSet<CargoType> cargoTypesRequiringLicense = new HashSet<CargoType>
	{
		CargoType.CrudeOil,
		CargoType.Diesel,
		CargoType.Gasoline,
		CargoType.Methane,
		CargoType.ChemicalsIskar,
		CargoType.ChemicalsSperex,
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
	};

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
		var unlicensedCargoTypes = from cargoType in cargoTypesRequiringLicense
									where !IsLicensedForCargoType(cargoType)
									select cargoType;
		if (CargoTypes_Patches.CanCarContainOnlyTheseCargoTypes(carLivery, unlicensedCargoTypes.ToHashSet()))
		{
			// Not licensed for cargo types
			return false;
		}

		return true;
	}

	public static bool IsLicensedForLoco(TrainCarLivery carLivery)
	{
		LicenseManager licenseManager = LicenseManager.Instance;
		return CarTypes.IsTender(carLivery) && licenseManager.IsGeneralLicenseAcquired(TransitionHelpers.ToV2(GeneralLicenseType.SH282)) || licenseManager.IsLicensedForCar(carLivery);
	}
}
