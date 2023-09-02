using DV;
using DV.ThingTypes;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

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
            DVObjectModel types = Globals.G.Types;
            try
            {
                // Crew vehicles use the vanilla crew vehicle summoning logic, so they can't be purchased.
                var summoner = CommsRadio.Controller.crewVehicleControl;
                var garageCarSpawners = summoner.crewVehicleGarages;
                if (garageCarSpawners != null)
                {
                    foreach (var garageSpawner in garageCarSpawners)
                    {
                        DVOwnership.Log($"garageSpawner.garageCarLivery : {garageSpawner.garageCarLivery.name}");
                        unmanagedTypes.Add(types.TrainCarType_to_v2.FirstOrDefault(x => x.Value == garageSpawner.garageCarLivery).Key);//garageSpawner.garageCarLivery.v1);
                    }
                }
            }
            catch (Exception e) { DVOwnership.OnCriticalFailure(e, "banning crew vehicles from purchase"); }
        }
    }
}
