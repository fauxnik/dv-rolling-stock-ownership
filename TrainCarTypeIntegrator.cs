﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace DVOwnership
{
    internal static class TrainCarTypeIntegrator
    {
        public static IEnumerable<TrainCarType> AllCarTypes
        {
            get
            {
                if (memoCarTypes == null) { memoCarTypes = GetAllCarTypes(); }
                return memoCarTypes;
            }
        }

        private static IEnumerable<TrainCarType> memoCarTypes;

        private static IEnumerable<TrainCarType> GetAllCarTypes()
        {
            var vanillaTypes = Enum.GetValues(typeof(TrainCarType)).Cast<TrainCarType>();
            IEnumerable<TrainCarType> customTypes;
            if (TryPullCustomTypes(out customTypes))
            {
                return vanillaTypes.Concat(customTypes);
            }
            return vanillaTypes;
        }

        private static bool TryPullCustomTypes(out IEnumerable<TrainCarType> customTypes)
        {
            try
            {
                customTypes = PullCustomTypes();
                DVOwnership.Log($"Loaded {customTypes.Count()} custom cars.");
                return true;
            }
            catch
            {
                customTypes = null;
                DVOwnership.LogWarning("CustomCarLoader not present or encountered error, skipping.");
                return false;
            }
        }

        // needs to be a separate method for try block to catch dll load exceptions when DVCustomCarLoader is not installed
        private static IEnumerable<TrainCarType> PullCustomTypes()
        {
            return DVCustomCarLoader.CustomCarManager.CustomCarTypes.Select(car => car.CarType);
        }
    }
}
