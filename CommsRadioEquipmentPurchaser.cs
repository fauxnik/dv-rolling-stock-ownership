using DV;
using DV.InventorySystem;
using DV.PointSet;
using DV.ThingTypes;
using DV.Utils;
using DVOwnership.Patches;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVOwnership
{
	public class CommsRadioEquipmentPurchaser : MonoBehaviour, ICommsRadioMode
	{
		private static Dictionary<TrainCarType, TrainCarType> locomotiveForTender = new Dictionary<TrainCarType, TrainCarType>
		{
			{ TrainCarType.Tender, TrainCarType.LocoSteamHeavy }
		};

		public ButtonBehaviourType ButtonBehaviour { get; private set; }

		private const float POTENTIAL_TRACKS_RADIUS = 200f;
		private const float MAX_DISTANCE_FROM_TRACK_POINT = 3f;
		private const float TRACK_POINT_POSITION_Y_OFFSET = -1.75f;
		private const float SIGNAL_RANGE = 100f;
		private const float INVALID_DESTINATION_HIGHLIGHTER_DISTANCE = 20f;
		private const float UPDATE_TRACKS_PERIOD = 2.5f;

		private static Color laserColor = new Color(1f, 0f, 0.9f, 1f);
		public Color GetLaserBeamColor() { return laserColor; }

		[Header("Strings")]
		private const string MODE_NAME = "ROLLING STOCK";
		private const string CONTENT_MAINMENU = "Buy equipment?";
		private const string CONTENT_SELECT_CAR = "{0}\n${1}\n\n{2}";
		private const string CONTENT_SELECT_DESTINATION = "{0}\n{1}m\n\n{2}";
		private const string CONTENT_CONFIRM_PURCHASE = "Buy {0} for ${1}?\n\n{2}";
		private const string CONTENT_FRAGMENT_INSUFFICIENT_FUNDS = "Insufficient funds.";
		private const string ACTION_CONFIRM_SELECTION = "buy";
		private const string ACTION_CONFIRM_DESTINATION = "place";
		private const string ACTION_CONFIRM_PURCHASE = "confirm";
		private const string ACTION_CANCEL = "cancel";

		public void OverrideSignalOrigin(Transform signalOrigin) { this.signalOrigin = signalOrigin; }

#nullable disable // the mod logs a critical failure if these don't exist
		public Transform signalOrigin;
		public CommsRadioDisplay display;
		public Material validMaterial;
		public Material invalidMaterial;
		public ArrowLCD lcdArrow;
#nullable restore

		[Header("Sounds")]
		public AudioClip? spawnModeEnterSound;
		public AudioClip? spawnVehicleSound;
		public AudioClip? confirmSound;
		public AudioClip? cancelSound;
		public AudioClip? hoverOverCar;
		public AudioClip? warningSound;
		public AudioClip? moneyRemovedSound;

		[Header("Highlighters")]
#nullable disable // the mod logs a critical failure if these don't exist
		public GameObject destinationHighlighterGO;
		public GameObject directionArrowsHighlighterGO;
		private CarDestinationHighlighter destinationHighlighter;
#nullable restore
		private RaycastHit hit;
		private LayerMask trackMask;
		private LayerMask laserPointerMask;

		private List<TrainCarType>? carTypesAvailableForPurchase;
		private int selectedCarTypeIndex = 0;
		private GameObject? carPrefabToSpawn;
		private Bounds carBounds;
		private float carLength;
		private float carPrice;

		private bool spawnWithTrackDirection = true;
		private List<RailTrack> potentialTracks = new List<RailTrack>();
		private bool canSpawnAtPoint;
		private RailTrack? destinationTrack;
		private EquiPointSet.Point? closestPointOnDestinationTrack;
		private Coroutine? trackUpdateCoro;

		private bool isPurchaseConfirmed = true;

		private State state;
		protected enum State
		{
			NotActive,
			MainMenu,
			PickCar,
			PickDestination,
			ConfirmPurchase,
		}
		protected enum Action
		{
			Trigger,
			Increase,
			Decrease,
		}

		#region Unity Lifecycle

		public void Awake()
		{
			try
			{
				// Copy components from other radio modes
				var summoner = (CommsRadio.Controller?.crewVehicleControl) ?? throw new Exception("Crew vehicle radio mode could not be found!");

				signalOrigin = summoner.signalOrigin;
				display = summoner.display ?? throw new Exception("Comms radio display could not be found!");
				validMaterial = summoner.validMaterial ?? throw new Exception("Material for valid placement could not be found!");
				invalidMaterial = summoner.invalidMaterial ?? throw new Exception("Material for invalid placement could not be found!");
				lcdArrow = summoner.lcdArrow ?? throw new Exception("LCD arrow could not be found!");
				destinationHighlighterGO = summoner.destinationHighlighterGO ?? throw new Exception("Destination highlighter game object could not be found!");
				directionArrowsHighlighterGO = summoner.directionArrowsHighlighterGO ?? throw new Exception("Direction arrows highlighter game object could not be found!");

				spawnModeEnterSound = summoner.spawnModeEnterSound;
				spawnVehicleSound = summoner.spawnVehicleSound;
				confirmSound = summoner.confirmSound;
				cancelSound = summoner.cancelSound;
				hoverOverCar = summoner.hoverOverCar;
				warningSound = summoner.warningSound;
				moneyRemovedSound = summoner.moneyRemovedSound;
			}
			catch (Exception e) { DVOwnership.OnCriticalFailure(e, "copying radio components"); }

			if (!signalOrigin)
			{
				DVOwnership.LogWarning("signalOrigin on CommsRadioEquipmentPurchaser is missing. Using this.transform instead.");
				signalOrigin = transform;
			}

			if (!spawnModeEnterSound || !spawnVehicleSound || !confirmSound || !cancelSound || !hoverOverCar || !warningSound || !moneyRemovedSound)
			{
				DVOwnership.LogWarning("Some audio clips are missing. Some sounds won't be played!");
			}

			trackMask = LayerMask.GetMask(new string[] { "Default" });
			laserPointerMask = LayerMask.GetMask(new string[] { "Laser_Pointer_Target" });

			destinationHighlighter = new CarDestinationHighlighter(destinationHighlighterGO, directionArrowsHighlighterGO);
			LicenseManager lm = SingletonBehaviour<LicenseManager>.Instance;
		   lm.JobLicenseAcquired += OnLicenseAcquired;
		}

		public void Start()
		{
			// TODO: does anything go in here?
		}

		private void OnDestroy()
		{
			if (UnloadWatcher.isUnloading) { return; }

			destinationHighlighter.Destroy();
			destinationHighlighter = null;
		}

		#endregion

		#region ICommsRadioMode

		public void Enable() { TransitionToState(State.MainMenu); }

		public void Disable() { TransitionToState(State.NotActive); }

		public void SetStartingDisplay() { display.SetDisplay(MODE_NAME, CONTENT_MAINMENU); }

		public void OnUpdate()
		{
			bool isDisplayUpdateNeeded = false;
			bool hasAffordabilityChanged = HasAffordabilityChanged;

			switch (state)
			{
				case State.NotActive:
				case State.MainMenu:
					break;

				case State.PickDestination:
					if (potentialTracks.Count > 0 && Physics.Raycast(signalOrigin.position, signalOrigin.forward, out hit, SIGNAL_RANGE, trackMask))
					{
						var point = hit.point;
						foreach (var railTrack in potentialTracks)
						{
							var pointWithinRangeWithYOffset = RailTrack.GetPointWithinRangeWithYOffset(railTrack, point, MAX_DISTANCE_FROM_TRACK_POINT, TRACK_POINT_POSITION_Y_OFFSET);
							if (pointWithinRangeWithYOffset.HasValue)
							{
								destinationTrack = railTrack;
								var trackPoints = railTrack.GetPointSet(0f).points;
								var index = pointWithinRangeWithYOffset.Value.index;
								var closestSpawnablePoint = CarSpawner.FindClosestValidPointForCarStartingFromIndex(trackPoints, index, carBounds.extents);
								var flag = closestSpawnablePoint != null;
								if (canSpawnAtPoint != flag) { isDisplayUpdateNeeded = true; }
								canSpawnAtPoint = flag;
								if (canSpawnAtPoint) { closestPointOnDestinationTrack = closestSpawnablePoint; }
								else { closestPointOnDestinationTrack = pointWithinRangeWithYOffset; }
								HighlightClosestPointOnDestinationTrack();
								goto default;
							}
						}
					}
					if (canSpawnAtPoint) { isDisplayUpdateNeeded = true; }
					canSpawnAtPoint = false;
					destinationTrack = null;
					HighlightInvalidPoint();
					goto default;

				case State.ConfirmPurchase:
					if (hasAffordabilityChanged && destinationTrack != null) { HighlightClosestPointOnDestinationTrack(); }
					goto default;

				default:
					if (hasAffordabilityChanged) { isDisplayUpdateNeeded = true; }
					break;
			}

			if (isDisplayUpdateNeeded) { TransitionToState(state); }
		}

		public void OnUse()
		{
			TransitionToState(DispatchAction(Action.Trigger));
		}

		public bool ButtonACustomAction()
		{
			TransitionToState(DispatchAction(Action.Decrease));
			return true;
		}

		public bool ButtonBCustomAction()
		{
			TransitionToState(DispatchAction(Action.Increase));
			return true;
		}

		#endregion

		private void ClearFlags()
		{
			destinationTrack = null;
			canSpawnAtPoint = false;
			destinationHighlighter.TurnOff();
		}

		#region State Machine

		private State DispatchAction(Action action)
		{
			switch (state)
			{
				case State.MainMenu:
					switch (action)
					{
						case Action.Trigger:
							return State.PickCar;
						default:
							DVOwnership.LogError($"Unexpected state/action pair! state: {state}, action: {action}");
							return State.MainMenu;
					}

				case State.PickCar:
					switch (action)
					{
						case Action.Trigger:
							return CanAfford ? State.PickDestination : State.MainMenu;
						case Action.Increase:
							SelectNextCar();
							break;
						case Action.Decrease:
							SelectPrevCar();
							break;
					}
					return State.PickCar;

				case State.PickDestination:
					switch (action)
					{
						case Action.Trigger:
							return CanAfford && canSpawnAtPoint ? State.ConfirmPurchase : State.MainMenu;
						case Action.Increase:
						case Action.Decrease:
							ReverseSpawnDirection();
							break;
					}
					return State.PickDestination;

				case State.ConfirmPurchase:
					switch (action)
					{
						case Action.Trigger:
							return State.MainMenu;
						case Action.Increase:
						case Action.Decrease:
							ToggleConfirmation();
							break;
					}
					return State.ConfirmPurchase;
			}

			DVOwnership.LogError($"Reached end of DispatchAction without returning a new state. This should never happen! state: {state}, action: {action}");
			return state;
		}

		private void TransitionToState(State newState)
		{
			var oldState = state;
			state = newState;

			switch (newState)
			{
				case State.NotActive:
					ButtonBehaviour = ButtonBehaviourType.Regular;
					ClearFlags();
					trackUpdateCoro = null;
					StopAllCoroutines();
					return;

				case State.MainMenu:
					if (oldState == State.ConfirmPurchase && isPurchaseConfirmed && CanAfford)
					{
						// Completed purchase
						CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
						DeductFunds(carPrice);
						SpawnCar();
					}
					else if (oldState != State.NotActive)
					{
						// Canceled purchase
						CommsRadioController.PlayAudioFromRadio(cancelSound, transform);
					}
					ButtonBehaviour = ButtonBehaviourType.Regular;
					DisplayMainMenu();
					ClearFlags();
					return;

				case State.PickCar:
					if (oldState == State.MainMenu)
					{
						CommsRadioController.PlayAudioFromRadio(spawnModeEnterSound, transform);
						UpdateCarTypesAvailableForPurchase();
					}
					ButtonBehaviour = ButtonBehaviourType.Override;
					UpdateCarToSpawn();
					DisplayCarTypeAndPrice();
					return;

				case State.PickDestination:
					if (oldState == State.PickCar)
					{
						CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
						if (trackUpdateCoro == null) { trackUpdateCoro = StartCoroutine(PotentialTracksUpdateCoro()); }
					}
					ButtonBehaviour = ButtonBehaviourType.Override;
					DisplayCarTypeAndLength();
					return;

				case State.ConfirmPurchase:
					if (oldState == State.PickDestination)
					{
						CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
						StopAllCoroutines();
						isPurchaseConfirmed = true;
					}
					ButtonBehaviour = ButtonBehaviourType.Override;
					DisplayPurchaseConfirmation();
					return;
			}

			DVOwnership.LogError($"Reached end of TransitionToState while transitioning from {oldState} to {newState}. This should never happen!");
		}

		#endregion

		#region LCD Display

		private void DisplayMainMenu()
		{
			SetStartingDisplay();
			lcdArrow.TurnOff();
		}

		private void DisplayCarTypeAndPrice()
		{
			if (!(SelectedCarType is TrainCarType carType)) { return; }
			var content = string.Format(CONTENT_SELECT_CAR, Enum.GetName(typeof(TrainCarType), carType), carPrice.ToString("F0"), CanAfford ? "" : CONTENT_FRAGMENT_INSUFFICIENT_FUNDS);
			var action = CanAfford ? ACTION_CONFIRM_SELECTION : ACTION_CANCEL;
			display.SetContentAndAction(content, action);
			lcdArrow.TurnOff();
		}

		private void DisplayCarTypeAndLength()
		{
			if (!(SelectedCarType is TrainCarType carType)) { return; }
			var content = string.Format(CONTENT_SELECT_DESTINATION, Enum.GetName(typeof(TrainCarType), carType), carLength.ToString("F"), CanAfford ? "" : CONTENT_FRAGMENT_INSUFFICIENT_FUNDS);
			var action = CanAfford && canSpawnAtPoint ? ACTION_CONFIRM_DESTINATION : ACTION_CANCEL;
			display.SetContentAndAction(content, action);
			if (canSpawnAtPoint) { UpdateLCDRerailDirectionArrow(); }
			else { lcdArrow.TurnOff(); }
		}

		private void DisplayPurchaseConfirmation()
		{
			if (!(SelectedCarType is TrainCarType carType)) { return; }
			var content = string.Format(CONTENT_CONFIRM_PURCHASE, Enum.GetName(typeof(TrainCarType), carType), carPrice.ToString("F0"), CanAfford ? "" : CONTENT_FRAGMENT_INSUFFICIENT_FUNDS);
			var action = CanAfford && isPurchaseConfirmed ? ACTION_CONFIRM_PURCHASE : ACTION_CANCEL;
			display.SetContentAndAction(content , action);
			lcdArrow.TurnOff();
		}

		private void UpdateLCDRerailDirectionArrow()
		{
			if (!closestPointOnDestinationTrack.HasValue)
			{
				DVOwnership.OnCriticalFailure(new Exception("CommsRadioEquipmentPurchaser is missing closestPointOnDestinationTrack!"), "updating LCD rerail direction arrow");
				return;
			}
			bool flag = Mathf.Sin(Vector3.SignedAngle(spawnWithTrackDirection ? closestPointOnDestinationTrack.Value.forward : (-closestPointOnDestinationTrack.Value.forward), signalOrigin.forward, Vector3.up) * 0.0174532924f) <= 0f;
			lcdArrow.TurnOn(!flag);
		}

		#endregion

		#region Finances

		private bool CanAfford
		{
			get { return SingletonBehaviour<Inventory>.Instance.PlayerMoney >= carPrice; }
		}

		private bool _couldAfford;
		private bool HasAffordabilityChanged
		{
			get
			{
				var canAfford = CanAfford;
				var couldAfford = _couldAfford;
				_couldAfford = canAfford;
				return canAfford != couldAfford;
			}
		}

		private float CalculateCarPrice(TrainCarType carType)
		{
			DVObjectModel types = Globals.G.Types;
			var isLoco = CarTypes.IsLocomotive(types.TrainCarType_to_v2[carType]);
			var price = ResourceTypes.GetFullUnitPriceOfResource(ResourceType.Car_DMG, types.TrainCarType_to_v2[carType]);
			if (isLoco) { price = ScaleLocoPrice(price); }
			if (DVOwnership.Settings.isPriceScaledWithDifficulty) { price = ScalePriceBasedOnDifficulty(price, isLoco); }
#if DEBUG
			return 0;
#else
			return Mathf.Round(price);
#endif
		}

		private float ScaleLocoPrice(float price)
		{
			return price * 10f;
		}

		private float ScalePriceBasedOnDifficulty(float price, bool isLoco)
		{
			return price;
			// return GamePreferences.Get<CareerDifficultyValues>(Preferences.CareerDifficulty) switch
			// {
			// 	CareerDifficultyValues.HARDCORE => Mathf.Pow(price / 10_000f, 1.1f) * 10_000f,
			// 	CareerDifficultyValues.CASUAL => price / (isLoco ? 100f : 10f),
			// 	_ => price,
			// };
		}

		private void DeductFunds(float price)
		{
			SingletonBehaviour<Inventory>.Instance.RemoveMoney(price);
			if (moneyRemovedSound != null) { CommsRadioController.PlayAudioFromRadio(this.moneyRemovedSound, base.transform); }
		}

		#endregion

		#region TrainCar Selection

		private void SelectNextCar()
		{
			selectedCarTypeIndex++;
			if (selectedCarTypeIndex >= (carTypesAvailableForPurchase?.Count ?? 0)) { selectedCarTypeIndex = 0; }
		}

		private void SelectPrevCar()
		{
			selectedCarTypeIndex--;
			if (selectedCarTypeIndex < 0) { selectedCarTypeIndex = (carTypesAvailableForPurchase?.Count ?? 1) - 1; }
		}

		private TrainCarType? SelectedCarType { get { return carTypesAvailableForPurchase?[selectedCarTypeIndex]; } }

		private void UpdateCarToSpawn()
		{
			DVObjectModel types = Globals.G.Types;
			if (!(SelectedCarType is TrainCarType carType)) { return; }

			carPrice = CalculateCarPrice(carType);

			carPrefabToSpawn = types.TrainCarType_to_v2[carType].prefab;
			if (carPrefabToSpawn == null)
			{
				carPrice = float.PositiveInfinity;
				DVOwnership.LogError($"Couldn't load car prefab: {carType}! Won't be able to spawn this car.");
				return;
			}

			var trainCar = carPrefabToSpawn.GetComponent<TrainCar>();
			carBounds = trainCar.Bounds;
			carLength = trainCar.InterCouplerDistance;
		}

		public void UpdateCarTypesAvailableForPurchase()
		{
			DVObjectModel types = Globals.G.Types;
			var prevSelectedCarType = carTypesAvailableForPurchase?.Count > 0 ? SelectedCarType : TrainCarType.NotSet;
			var allowedCarTypes = from carType in TrainCarTypeIntegrator.AllCarTypes
								  where !UnmanagedTrainCarTypes.UnmanagedTypes.Contains(carType)
								  select carType;
			var licensedCarTypes = from carType in allowedCarTypes
								   where CarTypes.IsAnyLocomotiveOrTender(types.TrainCarType_to_v2[carType]) ? LicenseManager_Patches.IsLicensedForLoco(LocoForTender(carType)) : LicenseManager_Patches.IsLicensedForCar(carType)
								   select carType;
			carTypesAvailableForPurchase = licensedCarTypes.ToList();
			selectedCarTypeIndex = carTypesAvailableForPurchase.FindIndex(carType => carType == prevSelectedCarType);
			if (selectedCarTypeIndex == -1) { selectedCarTypeIndex = 0; }
		}

		private TrainCarType LocoForTender(TrainCarType carType)
		{
			return locomotiveForTender.ContainsKey(carType) ? locomotiveForTender[carType] : carType;
		}

		private void OnLicenseAcquired(JobLicenseType_v2 jobLicenses)
		{
			if (state == State.PickCar) { UpdateCarTypesAvailableForPurchase(); }
		}

		#endregion

		#region Track Selection

		private void UpdatePotentialTracks()
		{
			potentialTracks.Clear();
			for (float radius = POTENTIAL_TRACKS_RADIUS; potentialTracks.Count == 0 && radius <= 800f; radius += 40f)
			{
				if (radius > POTENTIAL_TRACKS_RADIUS) { DVOwnership.LogWarning($"No tracks in {radius} radius. Expanding radius."); }
				foreach (var railTrack in RailTrackRegistry.Instance.AllTracks)
				{
					if (RailTrack.GetPointWithinRangeWithYOffset(railTrack, transform.position, radius, 0f) != null)
					{
						potentialTracks.Add(railTrack);
					}
				}
			}
			if (potentialTracks.Count == 0) { DVOwnership.LogError("No nearby tracks found. Can't spawn rolling stock!"); }
		}

		private IEnumerator PotentialTracksUpdateCoro()
		{
			Vector3 lastUpdatedTracksWorldPosition = Vector3.positiveInfinity;
			while (true)
			{
				if ((transform.position - WorldMover.currentMove - lastUpdatedTracksWorldPosition).magnitude > 100f)
				{
					UpdatePotentialTracks();
					lastUpdatedTracksWorldPosition = transform.position - WorldMover.currentMove;
				}
				yield return WaitFor.Seconds(UPDATE_TRACKS_PERIOD);
			}
		}

		private void HighlightClosestPointOnDestinationTrack()
		{
			if (!closestPointOnDestinationTrack.HasValue)
			{
				DVOwnership.OnCriticalFailure(new Exception("CommsRadioEquipmentPurchaser is missing closestPointOnDestinationTrack!"), "highlighting closest point on destination track");
				return;
			}
			var position = (Vector3)closestPointOnDestinationTrack.Value.position + WorldMover.currentMove;
			var vector = closestPointOnDestinationTrack.Value.forward;
			if (!spawnWithTrackDirection) { vector *= -1f; }

			destinationHighlighter.Highlight(position, vector, carBounds, CanAfford && canSpawnAtPoint ? validMaterial : invalidMaterial);
		}

		private void HighlightInvalidPoint()
		{
			destinationHighlighter.Highlight(signalOrigin.position + signalOrigin.forward * INVALID_DESTINATION_HIGHLIGHTER_DISTANCE, signalOrigin.right, carBounds, invalidMaterial);
		}

		#endregion

		#region Car Spawning

		private void SpawnCar()
		{
			if (!canSpawnAtPoint) { return; }
			if (!closestPointOnDestinationTrack.HasValue)
			{
				DVOwnership.OnCriticalFailure(new Exception("CommsRadioEquipmentPurchaser is missing closestPointOnDestinationTrack!"), "spawning car");
				return;
			}

			Vector3 position = (Vector3) this.closestPointOnDestinationTrack.Value.forward + WorldMover.currentMove;
			var vector = closestPointOnDestinationTrack.Value.forward;
			vector = spawnWithTrackDirection ? vector : -vector;

			var trainCar = CarSpawner.Instance.SpawnCar(carPrefabToSpawn, destinationTrack, position, vector);
			if (trainCar == null)
			{
				DVOwnership.LogError($"Couldn't spawn {SelectedCarType}!");
				return;
			}

			CommsRadioController.PlayAudioFromCar(spawnVehicleSound, trainCar);
			SingletonBehaviour<RollingStockManager>.Instance.Add(Equipment.FromTrainCar(trainCar));
			SingletonBehaviour<UnusedTrainCarDeleter>.Instance.MarkForDelete(trainCar);
		}

		private void ReverseSpawnDirection()
		{
			spawnWithTrackDirection = !spawnWithTrackDirection;
		}

		private void ToggleConfirmation()
		{
			isPurchaseConfirmed = !isPurchaseConfirmed;
		}

		#endregion
	}
}
