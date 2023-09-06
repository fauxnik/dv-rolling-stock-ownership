using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using System.Collections.Generic;
using System.Linq;
using System;

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
		}

		private static Dictionary<TrainCarType, HashSet<CargoType>> loadableCargoTypesPerTrainCarType = new ();
		private static HashSet<CargoType> GetLoadableCargoTypesForCarType(TrainCarType carType)
		{
			if (!loadableCargoTypesPerTrainCarType.TryGetValue(carType, out HashSet<CargoType> loadableCargoTypes))
			{
				loadableCargoTypes = loadableCargoTypesPerTrainCarType[carType] = new ();
				foreach (CargoType cargoType in Enum.GetValues(typeof(CargoType)))
				{
					if (TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(TransitionHelpers.ToV2(carType).parentType))
					{
						loadableCargoTypes.Add(cargoType);
					}
				}
			}

			return loadableCargoTypes;
		}

		public static bool CanCarContainOnlyTheseCargoTypes(TrainCarType carType, HashSet<CargoType> cargoTypes)
		{
			IEnumerable<CargoType> supportedCargoTypes = GetLoadableCargoTypesForCarType(carType);
			return supportedCargoTypes.Count() == supportedCargoTypes.Intersect(cargoTypes).Count();
		}
	}
}
