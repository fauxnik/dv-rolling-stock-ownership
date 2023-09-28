using DV.Logic.Job;
using DV.JObjectExtstensions;
using DV.Simulation.Cars;
using DV.ThingTypes;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;


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
		private string? bogie1TrackID;

		private static readonly string BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY = "bog1PosOnTrack";
		private double bogie1PositionAlongTrack;

		private static readonly string BOGIE_1_DERAILED_SAVE_KEY = "bog1Derailed";
		private bool isBogie1Derailed;

		private static readonly string BOGIE_2_TRACK_ID_SAVE_KEY = "bog2TrackID";
		private string? bogie2TrackID;

		private static readonly string BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY = "bog2PosOnTrack";
		private double bogie2PositionAlongTrack;

		private static readonly string BOGIE_2_DERAILED_SAVE_KEY = "bog2Derailed";
		private bool isBogie2Derailed;

		private static readonly string COUPLED_FRONT_SAVE_KEY = "coupledF";
		private string? _carGuidCoupledFront;
		public string? CarGuidCoupledFront
		{
			get
			{
				//DVOwnership.LogDebug(() => $"Getting front coupling for {ID} / {CarGUID} (currently: {_carGuidCoupledFront})");
				return _carGuidCoupledFront;
			}
			private set
			{
				DVOwnership.LogDebug(() => $"Setting front coupling for {ID} / {CarGUID} to {value}.");
				_carGuidCoupledFront = value;
			}
		}
		public bool IsCoupledFront { get { return !string.IsNullOrEmpty(CarGuidCoupledFront); } }

		private static readonly string COUPLED_REAR_SAVE_KEY = "coupledR";
		private string? _carGuidCoupledRear;
		public string? CarGuidCoupledRear
		{
			get
			{
				//DVOwnership.LogDebug(() => $"Getting rear coupling for {ID} / {CarGUID} (currently: {_carGuidCoupledRear})");
				return _carGuidCoupledRear;
			}
			private set
			{
				DVOwnership.LogDebug(() => $"Setting rear coupling for {ID} / {CarGUID} to {value}.");
				_carGuidCoupledRear = value;
			}
		}
		public bool IsCoupledRear { get { return !string.IsNullOrEmpty(CarGuidCoupledRear); } }

		private static readonly string CAR_EXPLODED_SAVE_KEY = "exploded";
		private bool isExploded;

		private static readonly string LOADED_CARGO_SAVE_KEY = "loadedCargo";
		private CargoType loadedCargo;

		private static readonly string HANDBRAKE_SAVE_KEY = "handbrake";
		private float? handbrakeApplication;

		private static readonly string MAIN_RESERVOIR_PRESSURE_SAVE_KEY = "mainReservoir";
		private float? mainReservoirPressure;

		private static readonly string CONTROL_RESERVOIR_PRESSURE_SAVE_KEY = "controlReservoir";
		private float? controlReservoirPressure;

		private static readonly string BRAKE_CYLINDER_PRESSURE_SAVE_KEY = "brakeCylinder";
		private float? brakeCylinderPressure;

		private static readonly string CAR_STATE_SAVE_KEY = "carState";
		private JObject? carStateSave;

		private static readonly string SIM_CAR_STATE_SAVE_KEY = "simCarState";
		private JObject? simCarStateSave;

		private const string SPAWNED_SAVE_KEY = "spawned";
		private TrainCar? trainCar;
		public bool IsSpawned { get { return trainCar != null; } }
		public bool IsMarkedForDespawning { get; private set; } = false;
		public bool IsStationary
		{
			get
			{
				if (!IsSpawned) { return true; }
				return trainCar!.GetForwardSpeed() < STATIONARY_SPEED_EPSILON; // IsSpawned checks if trainCar is null
			}
		}

		private static readonly string DESTINATION_SAVE_KEY = "destination";
		public string? DestinationID { get; private set; }

		public Equipment(string id, string carGuid, TrainCarType type, Vector3 position, Quaternion rotation, string? bogie1TrackID, double bogie1PositionAlongTrack, bool isBogie1Derailed, string? bogie2TrackID, double bogie2PositionAlongTrack, bool isBogie2Derailed, string? carGuidCoupledFront, string? carGuidCoupledRear, bool isExploded, CargoType loadedCargo, float? handbrakeApplication, float? mainReservoirPressure, float? controlReservoirPressure, float? brakeCylinderPressure, JObject? carStateSave, JObject? simCarStateSave, TrainCar? trainCar)
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
			this.handbrakeApplication = handbrakeApplication;
			this.mainReservoirPressure = mainReservoirPressure;
			this.controlReservoirPressure = controlReservoirPressure;
			this.brakeCylinderPressure = brakeCylinderPressure;
			this.carStateSave = carStateSave;
			this.simCarStateSave = simCarStateSave;
			this.trainCar = trainCar;

			SetupCouplerEventHandlers(trainCar);
		}

		public static Equipment FromTrainCar(TrainCar trainCar)
		{
			DVOwnership.Log($"Creating equipment record from train car {trainCar.ID}.");
			UnusedTrainCarDeleter.Instance.MarkForDelete(trainCar);
			var bogie1 = trainCar.Bogies[0];
			var bogie2 = trainCar.Bogies[1];
			var carState = trainCar.GetComponent<CarStateSave>();
			var simCarState = trainCar.GetComponent<SimCarStateSave>();
			//var locoState = trainCar.GetComponent<LocoStateSave>();
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
				trainCar.isExploded,
				trainCar.logicCar.CurrentCargoTypeInCar,
				trainCar.brakeSystem.handbrakePosition,
				trainCar.brakeSystem.mainReservoirPressure,
				trainCar.brakeSystem.controlReservoirPressure,
				trainCar.brakeSystem.brakeCylinderPressure,
				carState?.GetCarStateSaveData(),
				simCarState?.GetStateSaveData(),
				trainCar);
		}

		public void Update(TrainCar trainCar, bool isBeingDespawned)
		{
			DVOwnership.Log($"Updating equipment record for train car {trainCar.ID}{(isBeingDespawned ? ", which is being despawned" : "")}.");
			var bogie1 = trainCar.Bogies[0];
			var bogie2 = trainCar.Bogies[1];
			var carState = trainCar.GetComponent<CarStateSave>();
			var simCarState = trainCar.GetComponent<SimCarStateSave>();
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
			// Coupler state is kept updated via event handlers.
			isExploded = trainCar.isExploded;
			// The cargo in a despawning train car gets dumped before this update method is called.
			if (!isBeingDespawned) { UpdateCargo(trainCar); }
			handbrakeApplication = trainCar.brakeSystem.handbrakePosition;
			mainReservoirPressure = trainCar.brakeSystem.mainReservoirPressure;
			controlReservoirPressure = trainCar.brakeSystem.controlReservoirPressure;
			brakeCylinderPressure = trainCar.brakeSystem.brakeCylinderPressure;
			carStateSave = carState?.GetCarStateSaveData();
			simCarStateSave = simCarState?.GetStateSaveData();
			this.trainCar = isBeingDespawned ? null : trainCar;

			if (isBeingDespawned)
			{
				RemoveCouplerEventHandlers(trainCar);
			}
		}

		private void UpdateCargo(TrainCar? trainCar)
		{
			if (trainCar == null) { return; }
			loadedCargo = trainCar.logicCar.CurrentCargoTypeInCar;
		}

		private void SetupCouplerEventHandlers(TrainCar? trainCar)
		{
			if (trainCar == null) { return; }

			// This guarantees the event handlers won't be registered more than once. See: https://stackoverflow.com/a/1104269
			RemoveCouplerEventHandlers(trainCar);

			DVOwnership.LogDebug(() => $"Setting up coupler event handlers for {ID}.");

			trainCar.frontCoupler.Coupled += UpdateFrontCoupler;
			trainCar.frontCoupler.Uncoupled += UpdateFrontCoupler;
			trainCar.rearCoupler.Coupled += UpdateRearCoupler;
			trainCar.rearCoupler.Uncoupled += UpdateRearCoupler;
		}

		private void RemoveCouplerEventHandlers(TrainCar? trainCar)
		{
			if (trainCar == null ) { return; }

			DVOwnership.LogDebug(() => $"Removing coupler event handlers for {ID}.");

			trainCar.frontCoupler.Coupled -= UpdateFrontCoupler;
			trainCar.frontCoupler.Uncoupled -= UpdateFrontCoupler;
			trainCar.rearCoupler.Coupled -= UpdateRearCoupler;
			trainCar.rearCoupler.Uncoupled -= UpdateRearCoupler;
		}

		private void UpdateFrontCoupler(object sender, object args)
		{
			if (IsMarkedForDespawning) { return; }

			if (args is CoupleEventArgs coupleArgs)
			{
				CarGuidCoupledFront = coupleArgs.otherCoupler.train?.CarGUID;
				return;
			}

			if (args is UncoupleEventArgs)
			{
				CarGuidCoupledFront = null;
				return;
			}
		}

		private void UpdateRearCoupler(object sender, object args)
		{
			if (IsMarkedForDespawning) { return; }

			if (args is CoupleEventArgs coupleArgs)
			{
				CarGuidCoupledRear = coupleArgs.otherCoupler.train?.CarGUID;
				return;
			}

			if (args is UncoupleEventArgs)
			{
				CarGuidCoupledRear = null;
				return;
			}
		}

		public void SetDestination(string? stationId)
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

			// We remove the coupler event handlers because the train cars will be uncoupled as they despawn, but we want to preseve their pre-despawn state.
			RemoveCouplerEventHandlers(trainCar);

			// We update cargo early because the train cars will be unloaded as the despawn, but we want to preserve their pre-despawn state.
			UpdateCargo(trainCar);

			IsMarkedForDespawning = true;
			return true;
		}

		public TrainCar Spawn()
		{
			if (IsSpawned)
			{
				DVOwnership.LogWarning($"Trying to spawn train car based on equipment record with ID {ID}, but it already exists!");
				return trainCar!; // IsSpawned checks if trainCar is null
			}

			DVOwnership.Log($"Spawning train car based on equipment record with ID {ID}.");
			var carPrefab = TrainCar.GetCarPrefab(CarType);
			var allTracks = new List<RailTrack>(RailTrackRegistry.Instance.AllTracks);
			var bogie1Track = isBogie1Derailed ? null : allTracks.Find(track => track.logicTrack.ID.FullID == bogie1TrackID);
			var bogie2Track = isBogie2Derailed ? null : allTracks.Find(track => track.logicTrack.ID.FullID == bogie2TrackID);
			trainCar = CarSpawner.Instance.SpawnLoadedCar(carPrefab, ID, CarGUID, false, position + WorldMover.currentMove, rotation, isBogie1Derailed, bogie1Track, bogie1PositionAlongTrack, isBogie2Derailed, bogie2Track, bogie2PositionAlongTrack, IsCoupledFront, IsCoupledRear);

			if (loadedCargo != CargoType.None) { trainCar.logicCar.LoadCargo(trainCar.cargoCapacity, loadedCargo, null); }
			if (isExploded) { TrainCarExplosion.UpdateModelToExploded(trainCar); }

			if (handbrakeApplication.HasValue) { trainCar.brakeSystem.SetHandbrakePosition(handbrakeApplication.Value); }
			if (mainReservoirPressure.HasValue) { trainCar.brakeSystem.SetMainReservoirPressure(mainReservoirPressure.Value); }
			if (controlReservoirPressure.HasValue) { trainCar.brakeSystem.SetControlReservoirPressure(controlReservoirPressure.Value); }
			if (brakeCylinderPressure.HasValue) { trainCar.brakeSystem.ForceTargetTrainBrakeCylinderPressure(brakeCylinderPressure.Value); }

			var carState = trainCar.GetComponent<CarStateSave>();
			if (carStateSave != null && carState != null) { carState.SetCarStateSaveData(carStateSave); }

			var simCarState = trainCar.GetComponent<SimCarStateSave>();
			if(simCarStateSave != null && simCarState != null) { simCarState.SetStateSaveData(simCarState.GetStateSaveData()); }

			IsMarkedForDespawning = false;
			UnusedTrainCarDeleter.Instance.MarkForDelete(trainCar);

			SetupCouplerEventHandlers(trainCar);

			return trainCar;
		}

		public TrainCar? GetTrainCar()
		{
			if (!IsSpawned)
			{
				DVOwnership.Log($"Trying to get the logic car of equipment record with ID {ID}, but it isn't spawned!");
				return null;
			}

			return trainCar;
		}

		public Car? GetLogicCar()
		{
			if (!IsSpawned)
			{
				DVOwnership.Log($"Trying to get the logic car of equipment record with ID {ID}, but it isn't spawned!");
				return null;
			}

			return trainCar!.logicCar; // IsSpawned checks if trainCar is null
		}

		public bool IsRecordOf(TrainCar? trainCar)
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
			if (IsSpawned) { Update(trainCar!, false); } // IsSpawned checks if trainCar is null

			// Train car position appears to be world absolute, so we have compare to the player's world absolute position
			// This is different from what UnusedTrainCarDeleter appears to be doing, but I'm not sure why
			return (position - PlayerManager.GetWorldAbsolutePlayerPosition()).sqrMagnitude;
		}

		public bool ExistsInTrainset(Trainset? trainset)
		{
			if (!IsSpawned || trainset == null) { return false; }
			return trainset.cars.Contains(trainCar);
		}

		public static Equipment FromSaveData(JObject data)
		{
			DVOwnership.LogDebug(() => $"Restoring data: {data}");
			string id = GetOrThrow<string>(data, ID_SAVE_KEY);
			string carGuid = GetOrThrow<string>(data, CAR_GUID_SAVE_KEY);
			bool isSpawned = GetOrThrow<bool>(data, SPAWNED_SAVE_KEY);
			DVOwnership.Log($"Restoring equipment record with ID {id} and spawn state is {(isSpawned ? "spawned" : "not spawned")} from save data.");
			TrainCar? trainCar = null;
			if (isSpawned)
			{
				var allCars = CarSpawner.Instance.AllCars;
				trainCar = allCars.Find(tc => tc.CarGUID == carGuid);
				if (trainCar == null)
				{
					DVOwnership.LogWarning($"Couldn't find train car for spawned equipment with ID {id}. Marking as not spawned.");
					isSpawned = false;
				}
			}

			SanitizeBogieData(
				data.GetString(BOGIE_1_TRACK_ID_SAVE_KEY),
				data.GetDouble(BOGIE_1_POSITION_ALONG_TRACK_SAVE_KEY),
				data.GetBool(BOGIE_1_DERAILED_SAVE_KEY),
				out var bogie1TrackID,
				out var bogie1PositionAlongTrack,
				out var bogie1Derailed
			);
			SanitizeBogieData(
				data.GetString(BOGIE_2_TRACK_ID_SAVE_KEY),
				data.GetDouble(BOGIE_2_POSITION_ALONG_TRACK_SAVE_KEY),
				data.GetBool(BOGIE_2_DERAILED_SAVE_KEY),
				out var bogie2TrackID,
				out var bogie2PositionAlongTrack,
				out var bogie2Derailed
			);

			var equipment = new Equipment(
				id,
				carGuid,
				(TrainCarType)GetOrThrow<int>(data, CAR_TYPE_SAVE_KEY),
				GetOrThrow<Vector3>(data, WORLD_POSITION_SAVE_KEY),
				Quaternion.Euler(GetOrThrow<Vector3>(data, WORLD_ROTATION_SAVE_KEY)),
				bogie1TrackID,
				bogie1PositionAlongTrack,
				bogie1Derailed,
				bogie2TrackID,
				bogie2PositionAlongTrack,
				bogie2Derailed,
				data.GetString(COUPLED_FRONT_SAVE_KEY),
				data.GetString(COUPLED_REAR_SAVE_KEY),
				data.GetBool(CAR_EXPLODED_SAVE_KEY) ?? false,
				(CargoType?)data.GetInt(LOADED_CARGO_SAVE_KEY) ?? CargoType.None,
				data.GetFloat(HANDBRAKE_SAVE_KEY),
				data.GetFloat(MAIN_RESERVOIR_PRESSURE_SAVE_KEY),
				data.GetFloat(CONTROL_RESERVOIR_PRESSURE_SAVE_KEY),
				data.GetFloat(BRAKE_CYLINDER_PRESSURE_SAVE_KEY),
				data.GetJObject(CAR_STATE_SAVE_KEY),
				data.GetJObject(SIM_CAR_STATE_SAVE_KEY),
				trainCar);
			string destination = data.GetString(DESTINATION_SAVE_KEY);
			if (!string.IsNullOrEmpty(destination)) { equipment.SetDestination(destination); }
			return equipment;
		}

		private static T GetOrThrow<T>(JObject data, string key)
		{
			if (!(data[key] is JToken token)) { throw new Exception($"Can't load this equipment from save data because it's missing property {key}."); }
			var type = typeof(T);
			if (token.Type != TypeToJTokenType(type)) { throw new Exception($"Can't load this equipment from save data because property {key} is type {token.Type} but should be type {type.Name}."); }
			if (!(token.ToObject<T>() is T value)) { throw new Exception($"Can't load this equipment from save data because the type cast of property {key} failed."); }
			return value;
		}

		private static JTokenType TypeToJTokenType(Type type)
		{
			return type.Name switch
			{
				nameof(Boolean) => JTokenType.Boolean,
				nameof(String) => JTokenType.String,
				nameof(Int16) => JTokenType.Integer,
				nameof(Int32) => JTokenType.Integer,
				nameof(Int64) => JTokenType.Integer,
				nameof(Decimal) => JTokenType.Float,
				nameof(Double) => JTokenType.Float,
				nameof(Vector3) => JTokenType.Object,
				nameof(JObject) => JTokenType.Object,
				_ => JTokenType.None,
			};
		}

		private static void SanitizeBogieData(string? trackID, double? position, bool? derailed, out string? sanitizedTrackID, out double sanitizedPosition, out bool sanitizedDerailed)
		{
			sanitizedDerailed = derailed ?? trackID == null || position == null || Mathd.Approximately((double)position, -1);
			if (sanitizedDerailed == true)
			{
				sanitizedTrackID = null;
				sanitizedPosition = -1;
			}
			else
			{
				sanitizedTrackID = trackID;
				sanitizedPosition = (double)position!; // sanitizedDerailed would be true if position was null
			}
		}

		public JObject GetSaveData()
		{
			DVOwnership.LogDebug(() => "We are in save data");
			if (IsSpawned) { Update(trainCar!, false); } // IsSpawned checks if trainCar is null

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
			if (handbrakeApplication.HasValue && handbrakeApplication > 0f)
			{
				data.SetFloat(HANDBRAKE_SAVE_KEY, handbrakeApplication.Value);
			}
			if (mainReservoirPressure.HasValue && mainReservoirPressure > 1.1f)
			{
				data.SetFloat(MAIN_RESERVOIR_PRESSURE_SAVE_KEY, mainReservoirPressure.Value);
			}
			if (controlReservoirPressure.HasValue && controlReservoirPressure > 1.1f)
			{
				data.SetFloat(CONTROL_RESERVOIR_PRESSURE_SAVE_KEY, controlReservoirPressure.Value);
			}
			if (brakeCylinderPressure.HasValue && brakeCylinderPressure > 1.1f)
			{
				data.SetFloat(BRAKE_CYLINDER_PRESSURE_SAVE_KEY, brakeCylinderPressure.Value);
			}
			data.SetInt(LOADED_CARGO_SAVE_KEY, (int)loadedCargo);
			data.SetJObject(CAR_STATE_SAVE_KEY, carStateSave);
			data.SetJObject(SIM_CAR_STATE_SAVE_KEY, simCarStateSave);
			data.SetBool(SPAWNED_SAVE_KEY, IsSpawned);
			data.SetString(DESTINATION_SAVE_KEY, DestinationID);

			return data;
		}
	}
}
