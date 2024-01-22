using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DV;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;

namespace RollingStockOwnership;

internal static class TrainCarLiveryIntegrator
{
	private static readonly HashSet<TrainCarType> bannedTypes = new HashSet<TrainCarType> {
		TrainCarType.NotSet,
		TrainCarType.LocoRailbus,
	};

	private static Dictionary<TrainCarLivery, TrainCarLivery> locomotiveForTender = new Dictionary<TrainCarLivery, TrainCarLivery>
	{
		{ TransitionHelpers.ToV2(TrainCarType.Tender), TransitionHelpers.ToV2(TrainCarType.LocoSteamHeavy) }
	};

	// TODO: how to get loco/tender associations from CCL?
	public static TrainCarLivery LocoForTender(TrainCarLivery carLivery)
	{
		return locomotiveForTender.ContainsKey(carLivery) ? locomotiveForTender[carLivery] : carLivery;
	}

	public static IEnumerable<TrainCarLivery> AllCarLiveries
	{
		get
		{
			memoCarLiveries ??= GetAllCarLiveries();
			return memoCarLiveries;
		}
	}

	private static IEnumerable<TrainCarLivery>? memoCarLiveries;

	private static IEnumerable<TrainCarLivery> GetAllCarLiveries()
	{
		IEnumerable<TrainCarLivery> vanillaTypes = from kvPair in Globals.G.Types.TrainCarType_to_v2
		       select kvPair.Value;
		vanillaTypes = vanillaTypes.Where(type => !bannedTypes.Contains(type.v1));
		if (TryPullCustomTypes(out var customTypes))
		{
			return vanillaTypes.Concat(customTypes);
		}
		return vanillaTypes;
	}

	private static bool TryPullCustomTypes([NotNullWhen(true)] out IEnumerable<TrainCarLivery>? customTypes)
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
	private static IEnumerable<TrainCarLivery> PullCustomTypes()
	{
		var customCarVariants = from carType_v2 in CCL.Importer.CarManager.CustomCarTypes select carType_v2.Variants;
		return from carVariant in customCarVariants
		       select (TrainCarLivery)carVariant;
	}
}
