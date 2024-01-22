using CommsRadioAPI;
using DV;
using DV.ThingTypes;
using HarmonyLib;
using System;
using System.Collections.Generic;

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
			if (AccessTools.Field(typeof(CommsRadioCrewVehicle), "garageCarSpawners").GetValue(summoner) is GarageCarSpawner[] garageCarSpawners)
			{
				foreach (var garageSpawner in garageCarSpawners)
				{
					unmanagedLiveries.Add(garageSpawner.GarageCarLivery);
				}
			}
		}
		catch (Exception e) { Main.OnCriticalFailure(e, "banning crew vehicles from purchase"); }
	}
}
