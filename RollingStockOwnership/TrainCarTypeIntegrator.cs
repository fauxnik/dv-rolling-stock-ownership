using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DV;
using DV.ThingTypes;

namespace RollingStockOwnership;

internal static class TrainCarTypeIntegrator
{
	private static readonly HashSet<TrainCarType> bannedTypes = new HashSet<TrainCarType> {
		TrainCarType.NotSet,
		TrainCarType.LocoRailbus,
	};

	private static Dictionary<TrainCarType, TrainCarType> locomotiveForTender = new Dictionary<TrainCarType, TrainCarType>
	{
		{ TrainCarType.Tender, TrainCarType.LocoSteamHeavy }
	};

	public static TrainCarType LocoForTender(TrainCarType carType)
	{
		return locomotiveForTender.ContainsKey(carType) ? locomotiveForTender[carType] : carType;
	}

	public static IEnumerable<TrainCarType> AllCarTypes
	{
		get
		{
			memoCarTypes ??= GetAllCarTypes();
			return memoCarTypes;
		}
	}

	private static IEnumerable<TrainCarType>? memoCarTypes;

	private static IEnumerable<TrainCarType> GetAllCarTypes()
	{
		// return from kvPair in Globals.G.Types.TrainCarType_to_v2
		//        select kvPair.Key;
		IEnumerable<TrainCarType> vanillaTypes = Enum.GetValues(typeof(TrainCarType)).Cast<TrainCarType>();
		vanillaTypes = vanillaTypes.Where(type => !bannedTypes.Contains(type));
		if (TryPullCustomTypes(out var customTypes))
		{
			return vanillaTypes.Concat(customTypes);
		}
		return vanillaTypes;
	}

	private static bool TryPullCustomTypes([NotNullWhen(true)] out IEnumerable<TrainCarType>? customTypes)
	{
		try
		{
			customTypes = PullCustomTypes();
			Main.Log($"Loaded {customTypes.Count()} custom car types.");
			return true;
		}
		catch (System.IO.FileNotFoundException)
		{
			customTypes = null;
			Main.Log("DVCustomCarLoader not installed, skipping.");
			return false;
		}
		catch (Exception ex)
		{
			customTypes = null;
			Main.LogError($"Unexpected exception thrown while loading custom car types:\n{ex}");
			return false;
		}
	}

	// needs to be a separate method for try block to catch dll load exceptions when DVCustomCarLoader is not installed
	private static IEnumerable<TrainCarType> PullCustomTypes()
	{
		throw new NotImplementedException("TODO: restore integration with Custom Car Loader");
		// return from livery in (from carType_v2 in CCL.Importer.CarManager.CustomCarTypes select carType_v2.Variants)
		//        select livery.id;
		// return DVCustomCarLoader.CustomCarManager.CustomCarTypes.Select(car => car.CarType);
	}
}
