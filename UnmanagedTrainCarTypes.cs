using DV;
using Harmony12;
using System;
using System.Collections.Generic;

namespace DVOwnership
{
	public class UnmanagedTrainCarTypes
	{
		public static HashSet<TrainCarType> UnmanagedTypes
		{
			get
			{
				if (unmanagedTypes.Count == 1) { SetUnmanagedTypes(); }
				return new HashSet<TrainCarType>(unmanagedTypes);
			}
		}

		private static HashSet<TrainCarType> unmanagedTypes = new HashSet<TrainCarType>
		{
			TrainCarType.NotSet,
			// Crew vehicle types are added by the SetUnmanagedTypes method
		};

		private static void SetUnmanagedTypes()
		{
			try
			{
				// Crew vehicles use the vanilla crew vehicle summoning logic, so they can't be purchased.
				var summoner = CommsRadio.Controller.crewVehicleControl;
				var garageCarSpawners = AccessTools.Field(typeof(CommsRadioCrewVehicle), "garageCarSpawners").GetValue(summoner) as GarageCarSpawner[];
				if (garageCarSpawners != null)
				{
					foreach (var garageSpawner in garageCarSpawners)
					{
						unmanagedTypes.Add(garageSpawner.locoType);
					}
				}
			}
			catch (Exception e) { DVOwnership.OnCriticalFailure(e, "banning crew vehicles from purchase"); }
		}
	}
}
