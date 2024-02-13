using CommsRadioAPI;
using DV;
using DV.ThingTypes;
using DV.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership;

public class UnmanagedTrainCarTypes
{
	public static HashSet<TrainCarType> UnmanagedTypes
	{
		get
		{
			if (!isInitialized)
			{
				AddCrewVehiclesToUnmanagedTypes();
				isInitialized = true;
			}

			return new HashSet<TrainCarType>(unmanagedTypes);
		}
	}

	private static bool isInitialized = false;
	private static HashSet<TrainCarType> unmanagedTypes = new HashSet<TrainCarType>
	{
		TrainCarType.NotSet,
		// Crew vehicle types are added by the SetUnmanagedTypes method
	};

	private static void AddCrewVehiclesToUnmanagedTypes()
	{
		try
		{
			// Crew vehicles use the vanilla crew vehicle summoning logic, so they can't be purchased when that mode is enabled.
			if (!ControllerAPI.IsVanillaModeEnabled(VanillaMode.SummonCrewVehicle)) { return; }
			if (!(ControllerAPI.GetVanillaMode(VanillaMode.SummonCrewVehicle) is CommsRadioCrewVehicle summoner)) { throw new Exception("Crew vehicle radio mode could not be found!"); }

			CarSpawner carSpawner = SingletonBehaviour<CarSpawner>.Instance;
			var garageCarLiveries = carSpawner.crewVehicleGarages.Select((GarageType_v2 garageType) => garageType.garageCarLivery);
			foreach (TrainCarLivery summonableLivery in garageCarLiveries.Union(carSpawner.vehiclesWithoutGarage))
			{
				if (summonableLivery == null) { continue; }

				unmanagedTypes.Add(summonableLivery.v1);
			}

			Main.LogDebug(() => $"Set unmanaged types: [{string.Join(", ", unmanagedTypes)}]");
		}
		catch (Exception e) { Main.OnCriticalFailure(e, "banning crew vehicles from purchase"); }
	}
}
