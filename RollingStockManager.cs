using DV;
using Harmony12;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace DVOwnership
{
    public class RollingStockManager : SingletonBehaviour<RollingStockManager>
    {
        private List<Equipment> registry = new List<Equipment>();

        public static new string AllowAutoCreate() { return "[DVOwnership_RollingStockManager]"; }

        public void Add(Equipment equipment)
        {
            DVOwnership.Log($"Adding equipment record with ID {equipment.ID}, which is {(equipment.IsSpawned ? "spawned" : "not spawned")}, to the rolling stock registry.");
            registry.Add(equipment);
        }

        public void Remove(Equipment equipment)
        {
            DVOwnership.Log($"Removing equipment record with ID {equipment.ID}, which is {(equipment.IsSpawned ? "spawned" : "not spawned")}, from the rolling stock registry.");
            registry.Remove(equipment);
        }

        public Equipment FindByTrainCar(TrainCar trainCar)
        {
            DVOwnership.Log($"Looking up equipment record from the rolling stock registry by train car.");
            var equipment = from eq in registry where eq.IsRecordOf(trainCar) select eq;
            var count = equipment.Count();
            if (count > 1) { DVOwnership.LogError($"Unexpected number of equipment records found! Expected 1 but found {count} for train car ID {equipment.First().ID}."); }
            return equipment.FirstOrDefault();
        }

        public void LoadSaveData(JArray data)
        {
            int countLoaded = 0;
            foreach(var token in data)
            {
                if (token.Type != JTokenType.Object) { continue; }

                registry.Add(Equipment.FromSaveData((JObject)token));
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

        [HarmonyPatch(typeof(CommsRadioCarDeleter), "OnCarToDeleteDestroy")]
        class CommsRadioCarDeleter_OnCarToDeleteDestroy_Patch
        {
            static void Postfix(TrainCar destroyedCar)
            {
                DVOwnership.Log($"Train car is being deleted. Attempting to remove it from the rolling stock registry.");

                var manager = Instance;
                var equipment = manager?.FindByTrainCar(destroyedCar);
                if (equipment == null)
                {
                    DVOwnership.LogWarning($"Equipment record not found in the rolling stock registry.");
                    return;
                }

                DVOwnership.Log($"Removing equipment with ID {equipment.ID} from the rolling stock registry.");

                manager.Remove(equipment);
            }
        }
    }
}
