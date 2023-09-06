using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DV.ThingTypes;

namespace DVOwnership
{
	internal static class TrainCarTypeIntegrator
	{
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
			var vanillaTypes = Enum.GetValues(typeof(TrainCarType)).Cast<TrainCarType>();
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
				DVOwnership.Log($"Loaded {customTypes.Count()} custom car types.");
				return true;
			}
			catch (System.IO.FileNotFoundException)
			{
				customTypes = null;
				DVOwnership.Log("DVCustomCarLoader not installed, skipping.");
				return false;
			}
			catch (Exception ex)
			{
				customTypes = null;
				DVOwnership.LogError($"Unexpected exception thrown while loading custom car types:\n{ex}");
				return false;
			}
		}

		// needs to be a separate method for try block to catch dll load exceptions when DVCustomCarLoader is not installed
		private static IEnumerable<TrainCarType> PullCustomTypes()
		{
			throw new NotImplementedException("TODO: restore integration with Custom Car Loader");
			// return DVCustomCarLoader.CustomCarManager.CustomCarTypes.Select(car => car.CarType);
		}
	}
}
