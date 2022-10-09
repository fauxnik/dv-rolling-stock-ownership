using DV.Logic.Job;
using DV.JObjectExtstensions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DVOwnership
{
    internal class Equipment
    {
        private static readonly string ID_SAVE_KEY = "id";
        private string id;

        private static readonly string CAR_GUID_SAVE_KEY = "carGuid";
        private string carGuid;

        private static readonly string CAR_TYPE_SAVE_KEY = "type";
        private TrainCarType type;

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
        private bool isCoupledFront;

        private static readonly string COUPLED_REAR_SAVE_KEY = "coupledR";
        private bool isCoupledRear;

        private static readonly string CAR_EXPLODED_SAVE_KEY = "exploded";
        private bool isExploded;

        private static readonly string LOADED_CARGO_SAVE_KEY = "loadedCargo";
        private CargoType loadedCargo;

        private static readonly string CAR_STATE_SAVE_KEY = "carState";
        private JObject carStateSave;

        private static readonly string LOCO_STATE_SAVE_KEY = "locoState";
        private JObject locoStateSave;

        private static readonly string SPAWNED_SAVE_KEY = "spawned";
        private TrainCar trainCar;
        private bool IsSpawned { get { return trainCar != null; } }

        public Equipment(string id, string carGuid, TrainCarType type, Vector3 position, Quaternion rotation, string bogie1TrackID, double bogie1PositionAlongTrack, bool isBogie1Derailed, string bogie2TrackID, double bogie2PositionAlongTrack, bool isBogie2Derailed, bool isCoupledFront, bool isCoupledRear, bool isExploded, CargoType loadedCargo, JObject carStateSave, JObject locoStateSave, TrainCar trainCar)
        {
            this.id = id;
            this.carGuid = carGuid;
            this.type = type;
            this.position = position;
            this.rotation = rotation;
            this.bogie1TrackID = bogie1TrackID;
            this.bogie1PositionAlongTrack = bogie1PositionAlongTrack;
            this.isBogie1Derailed = isBogie1Derailed;
            this.bogie2TrackID = bogie2TrackID;
            this.bogie2PositionAlongTrack = bogie2PositionAlongTrack;
            this.isBogie2Derailed = isBogie2Derailed;
            this.isCoupledFront = isCoupledFront;
            this.isCoupledRear = isCoupledRear;
            this.isExploded = isExploded;
            this.loadedCargo = loadedCargo;
            this.carStateSave = carStateSave;
            this.locoStateSave = locoStateSave;
            this.trainCar = trainCar;
        }

        public static Equipment FromTrainCar(TrainCar trainCar)
        {
            var bogie1 = trainCar.Bogies[0];
            var bogie2 = trainCar.Bogies[1];
            var carState = trainCar.GetComponent<CarStateSave>();
            var locoState = trainCar.GetComponent<LocoStateSave>();
            return new Equipment(
                trainCar.logicCar.ID,
                trainCar.logicCar.carGuid,
                trainCar.carType,
                trainCar.transform.position - WorldMover.currentMove,
                trainCar.transform.rotation,
                bogie1.HasDerailed ? null : bogie1.track.logicTrack.ID.FullID,
                bogie1.HasDerailed ? -1 : bogie1.traveller.Span,
                bogie1.HasDerailed,
                bogie2.HasDerailed ? null : bogie2.track.logicTrack.ID.FullID,
                bogie2.HasDerailed ? -1 : bogie2.traveller.Span,
                bogie2.HasDerailed,
                trainCar.frontCoupler.IsCoupled(),
                trainCar.rearCoupler.IsCoupled(),
                trainCar.useExplodedModel,
                trainCar.logicCar.CurrentCargoTypeInCar,
                carState?.GetCarStateSaveData(),
                locoState?.GetLocoStateSaveData(),
                trainCar);
        }

        public void Update(TrainCar trainCar, bool isBeingDeleted)
        {
            var bogie1 = trainCar.Bogies[0];
            var bogie2 = trainCar.Bogies[1];
            var carState = trainCar.GetComponent<CarStateSave>();
            var locoState = trainCar.GetComponent<LocoStateSave>();
            id = trainCar.logicCar.ID;
            carGuid = trainCar.logicCar.carGuid;
            type = trainCar.carType;
            position = trainCar.transform.position - WorldMover.currentMove;
            rotation = trainCar.transform.rotation;
            bogie1TrackID = bogie1.HasDerailed ? null : bogie1.track.logicTrack.ID.FullID;
            bogie1PositionAlongTrack = bogie1.HasDerailed ? -1 : bogie1.traveller.Span;
            isBogie1Derailed = bogie1.HasDerailed;
            bogie2TrackID = bogie2.HasDerailed ? null : bogie2.track.logicTrack.ID.FullID;
            bogie2PositionAlongTrack = bogie2.HasDerailed ? -1 : bogie2.traveller.Span;
            isBogie2Derailed = bogie2.HasDerailed;
            isCoupledFront = trainCar.frontCoupler.IsCoupled();
            isCoupledRear = trainCar.rearCoupler.IsCoupled();
            isExploded = trainCar.useExplodedModel;
            loadedCargo = trainCar.logicCar.CurrentCargoTypeInCar;
            carStateSave = carState?.GetCarStateSaveData();
            locoStateSave = locoState?.GetLocoStateSaveData();
            this.trainCar = isBeingDeleted ? null : trainCar;
        }

        public void Spawn()
        {
            if (IsSpawned)
            {
                DVOwnership.LogWarning($"Trying to spawn train car with ID {id}, but it already exists!");
                return;
            }

            var carPrefab = CarTypes.GetCarPrefab(type);
            var allTracks = new List<RailTrack>(RailTrackRegistry.AllTracks);
            var bogie1Track = isBogie1Derailed ? null : allTracks.Find(track => track.logicTrack.ID.FullID == bogie1TrackID);
            var bogie2Track = isBogie2Derailed ? null : allTracks.Find(track => track.logicTrack.ID.FullID == bogie2TrackID);
            trainCar = CarSpawner.SpawnLoadedCar(carPrefab, id, carGuid, false, position + WorldMover.currentMove, rotation, isBogie1Derailed, bogie1Track, bogie1PositionAlongTrack, isBogie2Derailed, bogie2Track, bogie2PositionAlongTrack, isCoupledFront, isCoupledRear);

            if (loadedCargo != CargoType.None) { trainCar.logicCar.LoadCargo(trainCar.cargoCapacity, loadedCargo, null); }
            if (isExploded) { TrainCarExplosion.UpdateTrainCarModelToExploded(trainCar); }

            var carState = trainCar.GetComponent<CarStateSave>();
            if (carStateSave != null && carState != null) { carState.SetCarStateSaveData(carStateSave); }

            var locoState = trainCar.GetComponent<LocoStateSave>();
            if(locoStateSave != null && locoState != null) { locoState.SetLocoStateSaveData(locoStateSave); }
        }

        public static Equipment FromSaveData(JObject data)
        {
            string id = data.GetString(ID_SAVE_KEY);
            bool isSpawned = data.GetBool(SPAWNED_SAVE_KEY).Value;
            TrainCar trainCar = null;
            if (isSpawned)
            {
                var allCars = SingletonBehaviour<CarSpawner>.Instance.GetCars();
                trainCar = allCars.Find(tc => tc.ID == id);
            }
            return new Equipment(
                id,
                data.GetString(CAR_GUID_SAVE_KEY),
                (TrainCarType)data.GetInt(CAR_TYPE_SAVE_KEY),
                data.GetVector3(WORLD_POSITION_SAVE_KEY).Value,
                Quaternion.Euler(data.GetVector3(WORLD_ROTATION_SAVE_KEY).Value),
                data.GetString(BOGIE_1_TRACK_ID_SAVE_KEY),
                data.GetDouble(BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY).Value,
                data.GetBool(BOGIE_1_DERAILED_SAVE_KEY).Value,
                data.GetString(BOGIE_2_TRACK_ID_SAVE_KEY),
                data.GetDouble(BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY).Value,
                data.GetBool(BOGIE_2_DERAILED_SAVE_KEY).Value,
                data.GetBool(COUPLED_FRONT_SAVE_KEY).Value,
                data.GetBool(COUPLED_REAR_SAVE_KEY).Value,
                data.GetBool(CAR_EXPLODED_SAVE_KEY).Value,
                (CargoType)data.GetInt(LOADED_CARGO_SAVE_KEY),
                data.GetJObject(CAR_STATE_SAVE_KEY),
                data.GetJObject(LOCO_STATE_SAVE_KEY),
                trainCar);
        }

        public JObject GetSaveData()
        {
            if (IsSpawned) { Update(trainCar, false); }

            var data = new JObject();
            data.SetString(ID_SAVE_KEY, id);
            data.SetString(CAR_GUID_SAVE_KEY, carGuid);
            data.SetInt(CAR_TYPE_SAVE_KEY, (int)type);
            data.SetVector3(WORLD_POSITION_SAVE_KEY, position);
            data.SetVector3(WORLD_ROTATION_SAVE_KEY, rotation.eulerAngles);
            data.SetString(BOGIE_1_TRACK_ID_SAVE_KEY, bogie1TrackID);
            data.SetDouble(BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY, bogie1PositionAlongTrack);
            data.SetBool(BOGIE_1_DERAILED_SAVE_KEY, isBogie1Derailed);
            data.SetString(BOGIE_2_TRACK_ID_SAVE_KEY, bogie2TrackID);
            data.SetDouble(BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY, bogie2PositionAlongTrack);
            data.SetBool(BOGIE_2_DERAILED_SAVE_KEY, isBogie2Derailed);
            data.SetBool(COUPLED_FRONT_SAVE_KEY, isCoupledFront);
            data.SetBool(COUPLED_REAR_SAVE_KEY, isCoupledRear);
            data.SetBool(CAR_EXPLODED_SAVE_KEY, isExploded);
            data.SetInt(LOADED_CARGO_SAVE_KEY, (int)loadedCargo);
            data.SetJObject(CAR_STATE_SAVE_KEY, carStateSave);
            data.SetJObject(LOCO_STATE_SAVE_KEY, locoStateSave);
            data.SetBool(SPAWNED_SAVE_KEY, IsSpawned);

            return data;
        }
    }
}
