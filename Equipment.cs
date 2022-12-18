using DV.Logic.Job;
using DV.JObjectExtstensions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DVOwnership
{
    public class Equipment
    {
        private static readonly float STATIONARY_SPEED_EPSILON = 0.3f; // taken from TransportTask.UpdateTaskState()

        private static readonly string ID_SAVE_KEY = "id";
        public string ID { get; private set; }

        private static readonly string CAR_GUID_SAVE_KEY = "carGuid";
        public string CarGUID { get; private set; }

        private static readonly string CAR_TYPE_SAVE_KEY = "type";
        public TrainCarType CarType { get; private set; }

        private static readonly string WORLD_POSITION_SAVE_KEY = "position";
        private Vector3 position;

        private static readonly string WORLD_ROTATION_SAVE_KEY = "rotation";
        private Quaternion rotation;

        private static readonly string BOGIE_1_TRACK_ID_SAVE_KEY = "bog1TrackID";
        private string bogie1TrackID;

        private static readonly string BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY = "bog1PosOnTrack";
        private double bogie1PositionAlongTrack;

        private static readonly string BOGIE_1_DERAILED_SAVE_KEY = "bog1Derailed";
        private bool isBogie1Derailed;

        private static readonly string BOGIE_2_TRACK_ID_SAVE_KEY = "bog2TrackID";
        private string bogie2TrackID;

        private static readonly string BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY = "bog2PosOnTrack";
        private double bogie2PositionAlongTrack;

        private static readonly string BOGIE_2_DERAILED_SAVE_KEY = "bog2Derailed";
        private bool isBogie2Derailed;

        private static readonly string COUPLED_FRONT_SAVE_KEY = "coupledF";
        private string _carGuidCoupledFront;
        public string CarGuidCoupledFront
        {
            get
            {
                DVOwnership.LogDebug(() => $"Getting front coupling for {ID} / {CarGUID} (currently: {_carGuidCoupledFront})");
                // Make sure coupler state is up-to-date before returning
                if (IsSpawned) { Update(trainCar, false); }
                return _carGuidCoupledFront;
            }
            private set
            {
                DVOwnership.LogDebug(() => $"Setting front coupling for {ID} / {CarGUID} to {value}");
                _carGuidCoupledFront = value;
        }
        }
        public bool IsCoupledFront { get { return !string.IsNullOrEmpty(CarGuidCoupledFront); } }

        private static readonly string COUPLED_REAR_SAVE_KEY = "coupledR";
        private string _carGuidCoupledRear;
        public string CarGuidCoupledRear
        {
            get
            {
                DVOwnership.LogDebug(() => $"Getting rear coupling for {ID} / {CarGUID} (currently: {_carGuidCoupledFront})");
                // Make sure coupler state is up-to-date before returning
                if (IsSpawned) { Update(trainCar, false); }
                return _carGuidCoupledRear;
            }
            private set
            {
                DVOwnership.LogDebug(() => $"Setting rear coupling for {ID} / {CarGUID} to {value}");
                _carGuidCoupledRear = value;
        }
        }
        public bool IsCoupledRear { get { return !string.IsNullOrEmpty(CarGuidCoupledRear); } }

        private static readonly string CAR_EXPLODED_SAVE_KEY = "exploded";
        private bool isExploded;

        private static readonly string LOADED_CARGO_SAVE_KEY = "loadedCargo";
        private CargoType loadedCargo;

        private static readonly string CAR_STATE_SAVE_KEY = "carState";
        private JObject carStateSave;

        private const string LOCO_STATE_SAVE_KEY = "locoState";
        private JObject locoStateSave;

        private const string SPAWNED_SAVE_KEY = "spawned";
        private TrainCar trainCar;
        public bool IsSpawned { get { return trainCar != null; } }
        public bool IsMarkedForDespawning { get; private set; } = false;
        public bool IsStationary
        {
            get
            {
                if (!IsSpawned) { return true; }
                return trainCar.GetForwardSpeed() < STATIONARY_SPEED_EPSILON;
            }
        }

        private static readonly string DESTINATION_SAVE_KEY = "destination";
        public string DestinationID { get; private set; }

        public Equipment(string id, string carGuid, TrainCarType type, Vector3 position, Quaternion rotation, string bogie1TrackID, double bogie1PositionAlongTrack, bool isBogie1Derailed, string bogie2TrackID, double bogie2PositionAlongTrack, bool isBogie2Derailed, string carGuidCoupledFront, string carGuidCoupledRear, bool isExploded, CargoType loadedCargo, JObject carStateSave, JObject locoStateSave, TrainCar trainCar)
        {
            DVOwnership.Log($"Creating equipment record from values with ID {id}.");
            this.ID = id;
            this.CarGUID = carGuid;
            this.CarType = type;
            this.position = position;
            this.rotation = rotation;
            this.bogie1TrackID = bogie1TrackID;
            this.bogie1PositionAlongTrack = bogie1PositionAlongTrack;
            this.isBogie1Derailed = isBogie1Derailed;
            this.bogie2TrackID = bogie2TrackID;
            this.bogie2PositionAlongTrack = bogie2PositionAlongTrack;
            this.isBogie2Derailed = isBogie2Derailed;
            this.CarGuidCoupledFront = carGuidCoupledFront;
            this.CarGuidCoupledRear = carGuidCoupledRear;
            this.isExploded = isExploded;
            this.loadedCargo = loadedCargo;
            this.carStateSave = carStateSave;
            this.locoStateSave = locoStateSave;
            this.trainCar = trainCar;
        }

        public static Equipment FromTrainCar(TrainCar trainCar)
        {
            DVOwnership.Log($"Creating equipment record from train car {trainCar.ID}.");
            var bogie1 = trainCar.Bogies[0];
            var bogie2 = trainCar.Bogies[1];
            var carState = trainCar.GetComponent<CarStateSave>();
            var locoState = trainCar.GetComponent<LocoStateSave>();
            return new Equipment(
                trainCar.ID,
                trainCar.CarGUID,
                trainCar.carType,
                trainCar.transform.position - WorldMover.currentMove,
                trainCar.transform.rotation,
                bogie1.HasDerailed ? null : bogie1.track.logicTrack.ID.FullID,
                bogie1.HasDerailed ? -1 : bogie1.traveller.Span,
                bogie1.HasDerailed,
                bogie2.HasDerailed ? null : bogie2.track.logicTrack.ID.FullID,
                bogie2.HasDerailed ? -1 : bogie2.traveller.Span,
                bogie2.HasDerailed,
                trainCar.frontCoupler.GetCoupled()?.train?.CarGUID,
                trainCar.rearCoupler.GetCoupled()?.train?.CarGUID,
                trainCar.useExplodedModel,
                trainCar.logicCar.CurrentCargoTypeInCar,
                carState?.GetCarStateSaveData(),
                locoState?.GetLocoStateSaveData(),
                trainCar);
        }

        public void Update(TrainCar trainCar, bool isBeingDespawned)
        {
            DVOwnership.Log($"Updating equipment record for train car {trainCar.ID}{(isBeingDespawned ? ", which is being despawned" : "")}.");
            var bogie1 = trainCar.Bogies[0];
            var bogie2 = trainCar.Bogies[1];
            var carState = trainCar.GetComponent<CarStateSave>();
            var locoState = trainCar.GetComponent<LocoStateSave>();
            ID = trainCar.ID;
            CarGUID = trainCar.CarGUID;
            CarType = trainCar.carType;
            position = trainCar.transform.position - WorldMover.currentMove;
            rotation = trainCar.transform.rotation;
            bogie1TrackID = bogie1.HasDerailed ? null : bogie1.track.logicTrack.ID.FullID;
            bogie1PositionAlongTrack = bogie1.HasDerailed ? -1 : bogie1.traveller.Span;
            isBogie1Derailed = bogie1.HasDerailed;
            bogie2TrackID = bogie2.HasDerailed ? null : bogie2.track.logicTrack.ID.FullID;
            bogie2PositionAlongTrack = bogie2.HasDerailed ? -1 : bogie2.traveller.Span;
            isBogie2Derailed = bogie2.HasDerailed;
            // If the train car is being despawned, the coupler state is untrustworthy. It must be updated by the PrepareForDespawning method instead.
            if (!IsMarkedForDespawning)
            {
            CarGuidCoupledFront = trainCar.frontCoupler.GetCoupled()?.train?.CarGUID;
            CarGuidCoupledRear = trainCar.rearCoupler.GetCoupled()?.train?.CarGUID;
            }
            isExploded = trainCar.useExplodedModel;
            loadedCargo = trainCar.logicCar.CurrentCargoTypeInCar;
            carStateSave = carState?.GetCarStateSaveData();
            locoStateSave = locoState?.GetLocoStateSaveData();
            this.trainCar = isBeingDespawned ? null : trainCar;
        }

        public void SetDestination(string stationId)
        {
            DestinationID = stationId;
        }

        public bool PrepareForDespawning()
        {
            if (!IsSpawned)
            {
                DVOwnership.LogError($"Attempting to prepare equipment with ID {ID} for despawning, but it is not currently known to be spawned!");
                return false;
            }

            DVOwnership.Log($"Preparing equipment record with ID {ID} for despawning.");
            // We must update the coupler state here because it will be rendered untrustworthy before the Equipment#Update method gets called by the TrainCar#PrepareForDestroy patch.
            CarGuidCoupledFront = trainCar.frontCoupler.GetCoupled()?.train?.CarGUID;
            CarGuidCoupledRear = trainCar.rearCoupler.GetCoupled()?.train?.CarGUID;
            IsMarkedForDespawning = true;
            return true;
        }

        public TrainCar Spawn()
        {
            if (IsSpawned)
            {
                DVOwnership.LogWarning($"Trying to spawn train car based on equipment record with ID {ID}, but it already exists!");
                return trainCar;
            }

            DVOwnership.Log($"Spawning train car based on equipment record with ID {ID}.");
            var carPrefab = CarTypes.GetCarPrefab(CarType);
            var allTracks = new List<RailTrack>(RailTrackRegistry.AllTracks);
            var bogie1Track = isBogie1Derailed ? null : allTracks.Find(track => track.logicTrack.ID.FullID == bogie1TrackID);
            var bogie2Track = isBogie2Derailed ? null : allTracks.Find(track => track.logicTrack.ID.FullID == bogie2TrackID);
            trainCar = CarSpawner.SpawnLoadedCar(carPrefab, ID, CarGUID, false, position + WorldMover.currentMove, rotation, isBogie1Derailed, bogie1Track, bogie1PositionAlongTrack, isBogie2Derailed, bogie2Track, bogie2PositionAlongTrack, IsCoupledFront, IsCoupledRear);

            if (loadedCargo != CargoType.None) { trainCar.logicCar.LoadCargo(trainCar.cargoCapacity, loadedCargo, null); }
            if (isExploded) { TrainCarExplosion.UpdateTrainCarModelToExploded(trainCar); }

            var carState = trainCar.GetComponent<CarStateSave>();
            if (carStateSave != null && carState != null) { carState.SetCarStateSaveData(carStateSave); }

            var locoState = trainCar.GetComponent<LocoStateSave>();
            if(locoStateSave != null && locoState != null) { locoState.SetLocoStateSaveData(locoStateSave); }

            IsMarkedForDespawning = false;
            SingletonBehaviour<UnusedTrainCarDeleter>.Instance.MarkForDelete(trainCar);

            return trainCar;
        }

        public TrainCar GetTrainCar()
        {
            if (!IsSpawned)
            {
                DVOwnership.Log($"Trying to get the logic car of equipment record with ID {ID}, but it isn't spawned!");
                return null;
            }

            return trainCar;
        }

        public Car GetLogicCar()
        {
            if (!IsSpawned)
            {
                DVOwnership.Log($"Trying to get the logic car of equipment record with ID {ID}, but it isn't spawned!");
                return null;
            }

            return trainCar.logicCar;
        }

        public bool IsRecordOf(TrainCar trainCar)
        {
            if (trainCar == null)
            {
                DVOwnership.LogWarning("Train car is null. Can't compare equipment record. Returning false.");
                return false;
            }
            if (trainCar.logicCar != null) { return CarGUID == trainCar.CarGUID; }
            if (IsSpawned) { return this.trainCar == trainCar; }
            DVOwnership.LogWarning($"Trying to compare an unspawned equipment record with ID {ID} and a train car without a logic car. Returning false, but this may have unexpected side effects.");
            return false;
        }

        public bool IsCoupledTo(string carGuid)
        {
            return carGuid == CarGuidCoupledFront || carGuid == CarGuidCoupledRear;
        }

        public bool IsOnTrack(Track track)
        {
            return bogie1TrackID == track.ID.FullID || bogie2TrackID == track.ID.FullID;
        }

        public float SquaredDistanceFromPlayer()
        {
            // Make sure position is up-to-date before comparing
            if (IsSpawned) { Update(trainCar, false); }

            // Train car position appears to be world absolute, so we have compare to the player's world absolute position
            // This is different from what UnusedTrainCarDeleter appears to be doing, but I'm not sure why
            return (position - PlayerManager.GetWorldAbsolutePlayerPosition()).sqrMagnitude;
        }

        public bool ExistsInTrainset(Trainset trainset)
        {
            if (!IsSpawned || trainset == null) { return false; }
            return trainset.cars.Contains(trainCar);
        }

        public static Equipment FromSaveData(JObject data)
        {
            string id = data.GetString(ID_SAVE_KEY);
            string carGuid = data.GetString(CAR_GUID_SAVE_KEY);
            bool isSpawned = data.GetBool(SPAWNED_SAVE_KEY).Value;
            DVOwnership.Log($"Restoring equipment record with ID {id} and spawn state is {(isSpawned ? "spawned" : "not spawned")} from save data.");
            TrainCar trainCar = null;
            if (isSpawned)
            {
                var allCars = SingletonBehaviour<CarSpawner>.Instance.GetCars();
                trainCar = allCars.Find(tc => tc.CarGUID == carGuid);
            }
            var equipment = new Equipment(
                id,
                carGuid,
                (TrainCarType)data.GetInt(CAR_TYPE_SAVE_KEY),
                data.GetVector3(WORLD_POSITION_SAVE_KEY).Value,
                Quaternion.Euler(data.GetVector3(WORLD_ROTATION_SAVE_KEY).Value),
                data.GetString(BOGIE_1_TRACK_ID_SAVE_KEY),
                data.GetDouble(BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY).Value,
                data.GetBool(BOGIE_1_DERAILED_SAVE_KEY).Value,
                data.GetString(BOGIE_2_TRACK_ID_SAVE_KEY),
                data.GetDouble(BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY).Value,
                data.GetBool(BOGIE_2_DERAILED_SAVE_KEY).Value,
                data.GetString(COUPLED_FRONT_SAVE_KEY),
                data.GetString(COUPLED_REAR_SAVE_KEY),
                data.GetBool(CAR_EXPLODED_SAVE_KEY).Value,
                (CargoType)data.GetInt(LOADED_CARGO_SAVE_KEY),
                data.GetJObject(CAR_STATE_SAVE_KEY),
                data.GetJObject(LOCO_STATE_SAVE_KEY),
                trainCar);
            string destination = data.GetString(DESTINATION_SAVE_KEY);
            if (!string.IsNullOrEmpty(destination)) { equipment.SetDestination(destination); }
            return equipment;
        }

        public JObject GetSaveData()
        {
            if (IsSpawned) { Update(trainCar, false); }

            var data = new JObject();
            data.SetString(ID_SAVE_KEY, ID);
            data.SetString(CAR_GUID_SAVE_KEY, CarGUID);
            data.SetInt(CAR_TYPE_SAVE_KEY, (int)CarType);
            data.SetVector3(WORLD_POSITION_SAVE_KEY, position);
            data.SetVector3(WORLD_ROTATION_SAVE_KEY, rotation.eulerAngles);
            data.SetString(BOGIE_1_TRACK_ID_SAVE_KEY, bogie1TrackID);
            data.SetDouble(BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY, bogie1PositionAlongTrack);
            data.SetBool(BOGIE_1_DERAILED_SAVE_KEY, isBogie1Derailed);
            data.SetString(BOGIE_2_TRACK_ID_SAVE_KEY, bogie2TrackID);
            data.SetDouble(BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY, bogie2PositionAlongTrack);
            data.SetBool(BOGIE_2_DERAILED_SAVE_KEY, isBogie2Derailed);
            data.SetString(COUPLED_FRONT_SAVE_KEY, CarGuidCoupledFront);
            data.SetString(COUPLED_REAR_SAVE_KEY, CarGuidCoupledRear);
            data.SetBool(CAR_EXPLODED_SAVE_KEY, isExploded);
            data.SetInt(LOADED_CARGO_SAVE_KEY, (int)loadedCargo);
            data.SetJObject(CAR_STATE_SAVE_KEY, carStateSave);
            data.SetJObject(LOCO_STATE_SAVE_KEY, locoStateSave);
            data.SetBool(SPAWNED_SAVE_KEY, IsSpawned);
            data.SetString(DESTINATION_SAVE_KEY, DestinationID);

            return data;
        }
    }
}
