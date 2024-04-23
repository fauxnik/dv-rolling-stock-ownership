using DV.Logic.Job;
using RollingStockOwnership.Patches;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership;

public class RollingStockManager
{
	private static RollingStockManager? instance = null;
	public static RollingStockManager Instance { get => instance ??= new RollingStockManager(); }

	public static readonly object syncLock = new object();

	private List<Equipment> registry = new List<Equipment>();
	public List<Equipment> AllEquipment { get { return new List<Equipment>(registry); } }

	public void Add(Equipment equipment)
	{
		Main.Log($"Adding equipment record with ID {equipment.ID}, which is {(equipment.IsSpawned ? "spawned" : "not spawned")}, to the rolling stock registry.");
		registry.Add(equipment);
		IdGenerator_Patches.RegisterCarId(equipment.ID);
	}

	public void Remove(Equipment equipment)
	{
		Main.Log($"Removing equipment record with ID {equipment.ID}, which is {(equipment.IsSpawned ? "spawned" : "not spawned")}, from the rolling stock registry.");
		registry.Remove(equipment);
		IdGenerator_Patches.UnregisterCarId(equipment.ID);
	}

	public Equipment FindByTrainCar(TrainCar trainCar)
	{
		//Main.LogDebug(() => $"Looking up equipment record from the rolling stock registry by train car.");
		var equipment = from eq in registry where eq.IsRecordOf(trainCar) select eq;
		var count = equipment.Count();
		if (count != 1) { Main.LogError($"Unexpected number of equipment records found! Expected 1 but found {count} for train car ID {trainCar.ID}."); }
		return equipment.FirstOrDefault();
	}

	public Equipment? FindByCarGUID(string? carGuid)
	{
		//Main.LogDebug(() => $"Looking up equipment record from the rolling stock registry by car GUID {carGuid}.");
		var equipment = from eq in registry where eq.CarGUID == carGuid select eq;
		var count = equipment.Count();
		if (count != 1) { Main.LogError($"Unexpected number of equipment records found! Expected 1 but found {count} for car GUID {carGuid}."); }
		return equipment.FirstOrDefault();
	}

	public List<Equipment> GetConnectedEquipment(Equipment equipment)
	{
		var connectedEquipment = new List<Equipment>();
		var seenGuids = new HashSet<string>();
		var q = new Queue<Equipment>();
		q.Enqueue(equipment);

		while (q.Count > 0)
		{
			var next = q.Dequeue();
			connectedEquipment.Add(next);
			seenGuids.Add(next.CarGUID);

			var frontGuid = next.CarGuidCoupledFront;
			if (next.IsCoupledFront && !seenGuids.Contains(frontGuid!) && FindByCarGUID(frontGuid) is Equipment frontCar) // IsCoupledFront checks for null car guid
			{
				q.Enqueue(frontCar);
			}

			var rearGuid = next.CarGuidCoupledRear;
			if (next.IsCoupledRear && !seenGuids.Contains(rearGuid!) && FindByCarGUID(rearGuid) is Equipment rearCar) // IsCoupledRear checks for null car guid
			{
				q.Enqueue(rearCar);
			}
		}

		return connectedEquipment;
	}

	public List<Equipment> GetEquipmentOnTrack(Track track, bool? spawned = null)
	{
		var typeOfEquipment = spawned.HasValue ? spawned.Value ? "spawned " : "unspawned " : "";
		Main.Log($"Finding all {typeOfEquipment}equipment that is on track {track.ID.FullDisplayID}");
		var yto = YardTracksOrganizer.Instance;
		var equipments = from equipment in registry
							where equipment.IsOnTrack(track) && (!spawned.HasValue || equipment.IsSpawned == spawned)
							select equipment;
		return equipments.ToList();
	}

	// Simply calling registry.Clear() won't suffice because the IDs must also be removed from IdGenerator
	internal void Clear()
	{
		Main.Log($"Clearing rolling stock registry of {registry.Count} entries");

		// AllEquipment returns a new List, otherwise Remove will modify the enumerable under iteration
		foreach (Equipment equipment in AllEquipment)
		{
			Remove(equipment);
		}
	}

	public void LoadSaveData(JArray data)
	{
		int countLoaded = 0, countTotal = 0;
		foreach(var token in data)
		{
			if (token.Type != JTokenType.Object) { continue; }

			countTotal++;

			try
			{
				Equipment? loadedEquipment = Equipment.FromSaveData((JObject)token);
				if (loadedEquipment == null) { continue; }
				Add(loadedEquipment);
				countLoaded++;
			}
			catch (Exception exception)
			{
				// log the exception, but continue trying to load
				Main.LogWarning(exception.ToString());
			}
		}
		Main.Log($"Loaded {countLoaded}/{countTotal} equipment records into the rolling stock registry.");
	}

	public JArray GetSaveData()
	{
		var serializedRecords = from eq in registry select eq.GetSaveData();
		Main.Log($"Serialized {serializedRecords.Count()} equipment records from the rolling stock registry.");
		return new JArray(serializedRecords.ToArray());
	}

	// See CommsRadioCarDeleter_Patches for relevant patches
}
