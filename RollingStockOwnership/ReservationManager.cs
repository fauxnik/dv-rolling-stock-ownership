using DV.JObjectExtstensions;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
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

	// CarSpawner doesn't allow instance auto-creation, so this must be run on WorldStreamingInit.LoadingFinished
	internal static void SetupReservationCallbacks()
	{
		void CarSpawned(TrainCar wagon)
		{
			void CargoLoaded(CargoType _)
			{
				ReservationManager.Instance.Reserve(wagon);
			}

			wagon.CargoLoaded -= CargoLoaded;
			wagon.CargoLoaded += CargoLoaded;

			void CargoUnloaded()
			{
				ReservationManager.Instance.Release(wagon);
			}

			wagon.CargoUnloaded -= CargoUnloaded;
			wagon.CargoUnloaded += CargoUnloaded;
		}

		CarSpawner.Instance.CarSpawned -= CarSpawned;
		CarSpawner.Instance.CarSpawned += CarSpawned;
	}

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

	internal void Clear()
	{
		reservations.Clear();
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
				passesChecks &= reservation.CargoTypeID == car.CurrentCargoTypeInCar.ToV2().id;
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
			generatedJob.JobTaken += (job, _) => {
				if (job.jobType == JobType.Transport || job.jobType == JobType.ShuntingUnload)
				{
					ReservationManager.Instance.ForceReservations(__instance);
				}
			};
		}
	}

	internal JArray GetSaveData()
	{
		var serializedReservations = from reservation in reservations.Values select reservation.GetSaveData();
		Main.Log($"Serialized {serializedReservations.Count()} reservations from the reservation manager.");
		return new JArray(serializedReservations.ToArray());
	}

	internal void LoadSaveData(JArray data)
	{
		int countLoaded = 0, countTotal = 0;
		foreach(var token in data)
		{
			if (token.Type != JTokenType.Object) { continue; }

			countTotal++;

			try
			{
				Reservation? loadedReservation = Reservation.FromSaveData((JObject)token);
				if (loadedReservation == null) { continue; }
				string carGuid = loadedReservation.CarGuid;
				TrainCar? wagon = CarSpawner.Instance.AllCars.FirstOrDefault(wagon => wagon.logicCar.carGuid == carGuid);
				if (wagon == null || wagon.LoadedCargo.ToV2().id != loadedReservation.CargoTypeID) { continue; }
				reservations.Add(carGuid, loadedReservation);
				countLoaded++;
			}
			catch (Exception exception)
			{
				// log the exception, but continue trying to load
				Main.LogWarning(exception.ToString());
			}
		}
		Main.Log($"Loaded {countLoaded}/{countTotal} reservations into the reservation manager.");
	}
}

public class Reservation
{
	private const string CAR_GUID_SAVE_KEY = "carGuid";
	public readonly string CarGuid;
	private const string CARGO_TYPE_SAVE_KEY = "cargoType";
	public readonly string CargoTypeID;
	private const string OUTBOUND_YARD_SAVE_KEY = "outboundYard";
	public readonly string OutboundYardID;
	private const string INBOUND_YARD_SAVE_KEY = "inboundYard";
	public readonly string InboundYardID;

	public Reservation(Car car, StationsChainData stations)
	{
		CarGuid = car.carGuid;
		CargoTypeID = car.CurrentCargoTypeInCar.ToV2().id;
		OutboundYardID = stations.chainOriginYardId;
		InboundYardID = stations.chainDestinationYardId;
	}

	private Reservation(string carGuid, string cargoTypeID, string outboundYardID, string inboundYardID)
	{
		CarGuid = carGuid;
		CargoTypeID = cargoTypeID;
		OutboundYardID = outboundYardID;
		InboundYardID = inboundYardID;
	}

	public JObject GetSaveData()
	{
		var data = new JObject
		{
			{ CAR_GUID_SAVE_KEY, CarGuid },
			{ CARGO_TYPE_SAVE_KEY, CargoTypeID },
			{ OUTBOUND_YARD_SAVE_KEY, OutboundYardID },
			{ INBOUND_YARD_SAVE_KEY, InboundYardID }
		};
		return data;
	}

	public static Reservation FromSaveData(JObject data)
	{
		string carGuid = data.GetString(CAR_GUID_SAVE_KEY);
		string cargoTypeID = data.GetString(CARGO_TYPE_SAVE_KEY);
		string outboundYardID = data.GetString(OUTBOUND_YARD_SAVE_KEY);
		string inboundYardID = data.GetString(INBOUND_YARD_SAVE_KEY);
		return new Reservation(carGuid, cargoTypeID, outboundYardID, inboundYardID);
	}

	public static string ToString(Reservation reservation, string? wagonID = null)
	{
		wagonID ??= reservation.CarGuid;
		return $"[{wagonID} | {reservation.CargoTypeID} | {reservation.OutboundYardID} -> {reservation.InboundYardID}]";
	}
}
