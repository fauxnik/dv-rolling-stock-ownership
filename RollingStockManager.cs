using DV.Logic.Job;
using DVOwnership.Patches;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace DVOwnership
{
    public class RollingStockManager : SingletonBehaviour<RollingStockManager>
    {
        public static new string AllowAutoCreate() { return "[DVOwnership_RollingStockManager]"; }

        private List<Equipment> registry = new List<Equipment>();
        public List<Equipment> AllEquipment { get { return new List<Equipment>(registry); } }

        public void Add(Equipment equipment)
        {
            DVOwnership.Log($"Adding equipment record with ID {equipment.ID}, which is {(equipment.IsSpawned ? "spawned" : "not spawned")}, to the rolling stock registry.");
            registry.Add(equipment);
            IdGenerator_Patches.RegisterCarId(equipment.ID);
        }

        public void Remove(Equipment equipment)
        {
            DVOwnership.Log($"Removing equipment record with ID {equipment.ID}, which is {(equipment.IsSpawned ? "spawned" : "not spawned")}, from the rolling stock registry.");
            registry.Remove(equipment);
            IdGenerator_Patches.UnregisterCarId(equipment.ID);
        }

        public Equipment FindByTrainCar(TrainCar trainCar)
        {
            DVOwnership.LogDebug(() => $"Looking up equipment record from the rolling stock registry by train car.");
            var equipment = from eq in registry where eq.IsRecordOf(trainCar) select eq;
            var count = equipment.Count();
            if (count != 1) { DVOwnership.LogError($"Unexpected number of equipment records found! Expected 1 but found {count} for train car ID {trainCar.ID}."); }
            return equipment.FirstOrDefault();
        }

        public Equipment FindByCarGUID(string carGuid)
        {
            DVOwnership.LogDebug(() => $"Looking up equipment record from the rolling stock registry by car GUID {carGuid}.");
            var equipment = from eq in registry where eq.CarGUID == carGuid select eq;
            var count = equipment.Count();
            if (count != 1) { DVOwnership.LogError($"Unexpected number of equipment records found! Expected 1 but found {count} for car GUID {carGuid}."); }
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
                if (next.IsCoupledFront && !seenGuids.Contains(frontGuid)) { q.Enqueue(FindByCarGUID(frontGuid)); }

                var rearGuid = next.CarGuidCoupledRear;
                if (next.IsCoupledRear && !seenGuids.Contains(rearGuid)) { q.Enqueue(FindByCarGUID(rearGuid)); }
            }

            return connectedEquipment;
        }

        public void RespawnEquipmentOnTrack(Track track)
        {
            DVOwnership.Log($"Spawning all equipment on track {track.ID.FullDisplayID}");
            foreach (var equipment in registry)
            {
                if (equipment.IsOnTrack(track) && !equipment.IsSpawned)
                {
                    equipment.Spawn();
                }
            }
        }

        public void LoadSaveData(JArray data)
        {
            int countLoaded = 0;
            foreach(var token in data)
            {
                if (token.Type != JTokenType.Object) { continue; }

                Add(Equipment.FromSaveData((JObject)token));
                countLoaded++;
            }
            DVOwnership.Log($"Loaded {countLoaded} equipment records into the rolling stock registry.");
        }

        public JArray GetSaveData()
        {
            var serializedRecords = from eq in registry select eq.GetSaveData();
            DVOwnership.Log($"Serialized {serializedRecords.Count()} equipment records from the rolling stock registry.");
            return new JArray(serializedRecords.ToArray());
        }

        // See CommsRadioCarDeleter_Patches for relevant patches
    }
}
