using DV.Logic.Job;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

	public void Reserve(JobChainController chainController)
	{
		StationsChainData chainData = chainController.currentJobInChain.chainData;
		foreach (TrainCar trainCar in chainController.trainCarsForJobChain)
		{
			Reserve(trainCar.logicCar, chainData);
		}
	}

	public bool Reserve(Car car, StationsChainData stationsData)
	{
		string outboundYardID = stationsData.chainOriginYardId;
		string inboundYardID = stationsData.chainDestinationYardId;
		if (reservations.TryGetValue(car.carGuid, out Reservation reservation))
		{
			Main.LogError($"Trying to create reservation [{car.ID} | {outboundYardID} -> {inboundYardID}] but {car.ID} is already reserved! [{car.ID} | {reservation.OutboundYardID} -> {reservation.InboundYardID}]");
			return false;
		}

		reservation = new Reservation(car.carGuid, outboundYardID, inboundYardID);
		reservations.Add(car.carGuid, reservation);

		return true;
	}

	public void Release(JobChainController chainController)
	{
		foreach (TrainCar trainCar in chainController.trainCarsForJobChain)
		{
			Release(trainCar.logicCar);
		}
	}

	public bool Release(Car car)
	{
		if (!reservations.TryGetValue(car.carGuid, out _))
		{
			Main.LogWarning($"Trying to clear reservation for {car.ID}, but no such reservation exists!");
			return false;
		}

		reservations.Remove(car.carGuid);
		return true;
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
	// TODO: Is more data needed? Perhaps cargo type or cargo group?

	public Reservation(string carGuid, string outboundYardID, string inboundYardID)
	{
		CarGuid = carGuid;
		OutboundYardID = outboundYardID;
		InboundYardID = inboundYardID;
	}
}
