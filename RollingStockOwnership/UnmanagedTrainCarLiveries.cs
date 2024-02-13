using CommsRadioAPI;
using DV;
using DV.ThingTypes;
using DV.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership;

public class UnmanagedTrainCarLiveries
{
	public static HashSet<TrainCarLivery> UnmanagedLiveries
	{
		get
		{
			if (unmanagedLiveries.Count <= 1) { SetUnmanagedTypes(); }
			return new HashSet<TrainCarLivery>(unmanagedLiveries);
		}
	}

	private static HashSet<TrainCarLivery> unmanagedLiveries = new HashSet<TrainCarLivery>
	{
		// Crew vehicle types are added by the SetUnmanagedTypes method
	};

	private static void SetUnmanagedTypes()
	{
		try
		{
			// Crew vehicles use the vanilla crew vehicle summoning logic, so they can't be purchased.
			if (!(ControllerAPI.GetVanillaMode(VanillaMode.SummonCrewVehicle) is CommsRadioCrewVehicle summoner)) { throw new Exception("Crew vehicle radio mode could not be found!"); }

			CarSpawner carSpawner = SingletonBehaviour<CarSpawner>.Instance;
			var garageCarLiveries = carSpawner.crewVehicleGarages.Select((GarageType_v2 garageType) => garageType.garageCarLivery);
			foreach (TrainCarLivery summonableLivery in garageCarLiveries.Union(carSpawner.vehiclesWithoutGarage))
			{
				if (summonableLivery == null) { continue; }

				unmanagedLiveries.Add(summonableLivery);
			}

			Main.LogDebug(() => $"Set unmanaged liveries: [{string.Join(", ", unmanagedLiveries.Select(livery => livery.name))}]");
		}
		catch (Exception e) { Main.OnCriticalFailure(e, "banning crew vehicles from purchase"); }
	}
}
