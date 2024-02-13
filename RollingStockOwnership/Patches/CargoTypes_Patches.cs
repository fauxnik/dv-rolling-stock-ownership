using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RollingStockOwnership.Patches;

public class CargoTypes_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to setup cargo types patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up CargoTypes patches.");

		isSetup = true;
	}

	private static Dictionary<TrainCarLivery, HashSet<CargoType>> loadableCargoTypesPerTrainCarLivery = new ();
	private static HashSet<CargoType> GetLoadableCargoTypesForCarLivery(TrainCarLivery carLivery)
	{

		if (!loadableCargoTypesPerTrainCarLivery.TryGetValue(carLivery, out HashSet<CargoType> loadableCargoTypes))
		{
			loadableCargoTypes = loadableCargoTypesPerTrainCarLivery[carLivery] = new ();

			TrainCarType_v2? carType = carLivery.parentType;
			if (carType == null) { return loadableCargoTypes; }

			foreach (CargoType cargoType in Enum.GetValues(typeof(CargoType)))
			{
				CargoType_v2? cargoType_v2 = TransitionHelpers.ToV2(cargoType);
				if (cargoType_v2 != null && cargoType_v2.IsLoadableOnCarType(carType))
				{
					loadableCargoTypes.Add(cargoType);
				}
			}
		}

		return loadableCargoTypes;
	}

	public static bool CanCarContainOnlyTheseCargoTypes(TrainCarLivery carLivery, HashSet<CargoType> cargoTypes)
	{
		IEnumerable<CargoType> supportedCargoTypes = GetLoadableCargoTypesForCarLivery(carLivery);
		return supportedCargoTypes.Count() == supportedCargoTypes.Intersect(cargoTypes).Count();
	}
}
