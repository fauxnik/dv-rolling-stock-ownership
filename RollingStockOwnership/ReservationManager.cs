using DV.Logic.Job;
using DV.ThingTypes;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace RollingStockOwnership;

public class ReservationManager
{
	private static ReservationManager? instance = null;
	public static ReservationManager Instance { get => instance ??= new ReservationManager(); }

	private Dictionary<string, Reservation> reservations = new Dictionary<string, Reservation>();

	public bool HasReservation(TrainCar trainCar)
	{
		return HasReservation(trainCar.logicCar);
	}

	public bool HasReservation(Car car)
	{
		return reservations.ContainsKey(car.carGuid);
	}

	private Reservation? GetReservation(Car car)
	{
		if (reservations.TryGetValue(car.carGuid, out Reservation reservation))
		{
			return reservation;
		}
		return null;
	}

	public bool TryGetReservation(TrainCar trainCar, [NotNullWhen(true)] out Reservation? reservation)
	{
		return TryGetReservation(trainCar.logicCar, out reservation);
	}

	public bool TryGetReservation(Car car, [NotNullWhen(true)] out Reservation? reservation)
	{
		reservation = GetReservation(car);
		return reservation != null;
	}

	public bool Reserve(TrainCar wagon)
	{
		var job = (Job?)JobsManager.Instance.GetJobOfCar(wagon);
		if (job?.State != JobState.InProgress) { return false; }
		return Reserve(wagon.logicCar, job.chainData);
	}

	private bool Reserve(Car car, StationsChainData stationsData)
	{
		var reservation = new Reservation(car, stationsData);

		if (reservations.TryGetValue(car.carGuid, out Reservation priorReservation))
		{
			Main.LogWarning(
				$"Overwriting existing reservation {Reservation.ToString(priorReservation, car.ID)} " +
				$"with new reservation {Reservation.ToString(reservation, car.ID)}"
			);
		}

		reservations[car.carGuid] = reservation;
		return true;
	}

	public bool Release(TrainCar wagon)
	{
		return Release(wagon.logicCar);
	}

	private bool Release(Car car)
	{
		if (!HasReservation(car))
		{
			Main.LogWarning($"Trying to clear reservation for {car.ID}, but no such reservation exists");
			return false;
		}

		reservations.Remove(car.carGuid);
		return true;
	}

	private void ForceReservations(JobChainController jobChainController)
	{
		Job job = jobChainController.currentJobInChain;

		if (!new JobType[] {
				JobType.Transport,
				JobType.ShuntingUnload
			}.Contains(job.jobType)) { return; }

		StationsChainData stationsData = job.chainData;
		foreach (TrainCar wagon in jobChainController.trainCarsForJobChain)
		{
			Car car = wagon.logicCar;

			if (TryGetReservation(car, out Reservation? reservation))
			{
				bool passesChecks = reservation.CarGuid == car.carGuid; // This should be true because we found a reservation, but it doesn't hurt to double check
				passesChecks &= reservation.OutboundYardID == stationsData.chainOriginYardId;
				passesChecks &= reservation.InboundYardID == stationsData.chainDestinationYardId;
				if (passesChecks) { continue; }
			}

			reservations[car.carGuid] = new Reservation(car, stationsData);
		}
	}

	[HarmonyPatch(typeof(JobChainController), "OnJobGenerated")]
	class JobChainController_OnJobGenerated_Patch
	{
		static void Prefix(Job generatedJob, JobChainController __instance)
		{
			generatedJob.JobTaken += (_, _) => {
				Instance.ForceReservations(__instance);
			};
		}
	}

	internal JArray GetSaveData()
	{
		throw new NotImplementedException("TODO: implement ReservationManager.GetSaveData()");
	}

	internal void LoadSaveData(JArray data)
	{
		throw new NotImplementedException("TODO: implement ReservationManager.LoadSaveData(data)");
	}
}

public class Reservation
{
	public readonly string CarGuid;
	public readonly string OutboundYardID;
	public readonly string InboundYardID;

	public Reservation(Car car, StationsChainData stations)
	{
		CarGuid = car.carGuid;
		OutboundYardID = stations.chainOriginYardId;
		InboundYardID = stations.chainDestinationYardId;
	}

	private Reservation(string carGuid, string outboundYardID, string inboundYardID)
	{
		CarGuid = carGuid;
		OutboundYardID = outboundYardID;
		InboundYardID = inboundYardID;
	}

	public static string ToString(Reservation reservation, string? wagonID = null)
	{
		wagonID ??= reservation.CarGuid;
		return $"[{wagonID} | {reservation.OutboundYardID} -> {reservation.InboundYardID}]";
	}
}
