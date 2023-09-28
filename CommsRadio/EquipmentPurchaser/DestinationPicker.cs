using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommsRadioAPI;
using DV;
using DV.PointSet;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using UnityEngine;

namespace DVOwnership.CommsRadio.EquipmentPurchaser;

internal class DestinationPicker : AStateBehaviour
{
	private static Coroutine? PotentialTracksUpdateCoroutine;
	private static CarDestinationHighlighter? cachedDestinationHighlighter;
	private static List<RailTrack> potentialTracks = new List<RailTrack>();
	private static LayerMask trackMask = LayerMask.GetMask(new string[] { "Default" });
	private const float POTENTIAL_TRACKS_RADIUS = 200f;
	private const float MAX_DISTANCE_FROM_TRACK_POINT = 3f;
	private const float TRACK_POINT_POSITION_Y_OFFSET = -1.75f;
	private const float SIGNAL_RANGE = 100f;
	private const float INVALID_DESTINATION_HIGHLIGHTER_DISTANCE = 20f;
	private const float UPDATE_TRACKS_PERIOD = 2.5f;

	private TrainCarType selectedCarType;
	private Bounds selectedCarBounds;
	private Transform signalOrigin;
	private RailTrack? selectedTrack;
	private EquiPointSet.Point? selectedPoint;
	private bool isSelectedOrientationOppositeTrackDirection;
	private CarDestinationHighlighter destinationHighlighter;

	public DestinationPicker(
		TrainCarType carType,
		Bounds carBounds,
		Transform signalOrigin,
		RailTrack? track = null,
		EquiPointSet.Point? spawnPoint = null,
		bool reverseDirection = false
	) : base(
		new CommsRadioState(
			titleText: "Rolling Stock",
			contentText: ContentFromType(carType),
			actionText: IsPlaceable(carType, track, spawnPoint) ? "place" : "cancel",
			arrowState: GetArrowState(signalOrigin, spawnPoint, reverseDirection),
			buttonBehaviour: ButtonBehaviourType.Override
		)
	) {
		selectedCarType = carType;
		selectedCarBounds = carBounds;
		this.signalOrigin = signalOrigin;
		selectedTrack = track;
		selectedPoint = spawnPoint;
		isSelectedOrientationOppositeTrackDirection = reverseDirection;
		if (cachedDestinationHighlighter != null)
		{
			destinationHighlighter = cachedDestinationHighlighter;
		}
		else
		{
			CommsRadioCrewVehicle? summoner = ControllerAPI.GetVanillaMode(VanillaMode.SummonCrewVehicle) as CommsRadioCrewVehicle;
			if (summoner == null) { throw new Exception("Couldn't find crew vehicle summoner mode."); }
			destinationHighlighter = cachedDestinationHighlighter = new CarDestinationHighlighter(summoner.destinationHighlighterGO, summoner.directionArrowsHighlighterGO);
		}
	}

	public override void OnEnter(CommsRadioUtility utility, AStateBehaviour? previous)
	{
		base.OnEnter(utility, previous);
		HighlightSpawnPoint(utility);
		if (!(previous is DestinationPicker))
		{
			if (PotentialTracksUpdateCoroutine != null)
				DVOwnership.LogError("Attempting to start potential track update coroutine when it's already running.");
			else
				PotentialTracksUpdateCoroutine = utility.StartCoroutine(StartPotentialTracksUpdateCoroutine());
		}
	}

	public override void OnLeave(CommsRadioUtility utility, AStateBehaviour? next)
	{
		base.OnLeave(utility, next);
		destinationHighlighter.TurnOff();
		if (!(next is DestinationPicker))
		{
			if (PotentialTracksUpdateCoroutine == null)
				DVOwnership.LogError("Attempting to stop potential track update coroutine when it's not running.");
			else
				utility.StopCoroutine(PotentialTracksUpdateCoroutine);
			PotentialTracksUpdateCoroutine = null;
		}
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		switch (action)
		{
			case InputAction.Activate:
				if (IsPlaceable(selectedCarType, selectedTrack, selectedPoint))
				{
					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
					return new PurchaseConfirmer(selectedCarType, selectedTrack, selectedPoint.Value, isSelectedOrientationOppositeTrackDirection, destinationHighlighter);
				}
				utility.PlaySound(VanillaSoundCommsRadio.Cancel);
				return new MainMenu();

			case InputAction.Up:
			case InputAction.Down:
				return new DestinationPicker(selectedCarType, selectedCarBounds, signalOrigin, selectedTrack, selectedPoint, !isSelectedOrientationOppositeTrackDirection);

			default:
				throw new Exception($"Unexpected action: {action}");
		}
	}

	public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
	{
		if (potentialTracks.Count > 0 && Physics.Raycast(signalOrigin.position, signalOrigin.forward, out RaycastHit hit, SIGNAL_RANGE, trackMask))
		{
			Vector3 point = hit.point;
			foreach (RailTrack railTrack in potentialTracks)
			{
				EquiPointSet.Point? pointWithinRangeWithYOffset = RailTrack.GetPointWithinRangeWithYOffset(railTrack, point, MAX_DISTANCE_FROM_TRACK_POINT, TRACK_POINT_POSITION_Y_OFFSET);
				if (pointWithinRangeWithYOffset.HasValue)
				{
					EquiPointSet.Point[] trackPoints = railTrack.GetPointSet(0f).points;
					int index = pointWithinRangeWithYOffset.Value.index;
					EquiPointSet.Point? closestSpawnablePoint = CarSpawner.FindClosestValidPointForCarStartingFromIndex(trackPoints, index, selectedCarBounds.extents);

					return new DestinationPicker(selectedCarType, selectedCarBounds, signalOrigin, railTrack, closestSpawnablePoint, isSelectedOrientationOppositeTrackDirection);
				}
			}
		}

		if (selectedTrack != null || selectedPoint != null)
		{
			return new DestinationPicker(selectedCarType, selectedCarBounds, signalOrigin, null, null, isSelectedOrientationOppositeTrackDirection);
		}

		// No transition will happen when returning `this`, thus we must manually update the destination highlighter.
		HighlightSpawnPoint(utility);
		return this;
	}

	private void UpdatePotentialTracks()
	{
		potentialTracks.Clear();
		for (float radius = POTENTIAL_TRACKS_RADIUS; potentialTracks.Count == 0 && radius <= 800f; radius += 40f)
		{
			if (radius > POTENTIAL_TRACKS_RADIUS) { DVOwnership.LogWarning($"No tracks in {radius - 40f} radius. Expanding radius."); }
			foreach (var railTrack in RailTrackRegistry.Instance.AllTracks)
			{
				if (RailTrack.GetPointWithinRangeWithYOffset(railTrack, signalOrigin.position, radius, 0f) != null)
				{
					potentialTracks.Add(railTrack);
				}
			}
		}
		if (potentialTracks.Count == 0) { DVOwnership.LogError("No nearby tracks found. Can't spawn rolling stock!"); }
	}

	private IEnumerator StartPotentialTracksUpdateCoroutine()
	{
		Vector3 lastUpdatedTracksWorldPosition = Vector3.positiveInfinity;
		while (true)
		{
			if ((signalOrigin.position - WorldMover.currentMove - lastUpdatedTracksWorldPosition).magnitude > SIGNAL_RANGE)
			{
				UpdatePotentialTracks();
				lastUpdatedTracksWorldPosition = signalOrigin.position - WorldMover.currentMove;
			}
			yield return WaitFor.Seconds(UPDATE_TRACKS_PERIOD);
		}
	}

	private void HighlightSpawnPoint(CommsRadioUtility utility)
	{
		Vector3 position, direction;
		Material? highlightMaterial;
		if (IsPlaceable(selectedCarType, selectedTrack, selectedPoint))
		{
			position = (Vector3)selectedPoint.Value.position + WorldMover.currentMove;
			direction = selectedPoint.Value.forward;
			if (isSelectedOrientationOppositeTrackDirection) { direction *= -1f; }
			highlightMaterial = utility.GetMaterial(VanillaMaterial.Valid);
		}
		else
		{
			position = signalOrigin.position + signalOrigin.forward * INVALID_DESTINATION_HIGHLIGHTER_DISTANCE;
			direction = signalOrigin.right;
			highlightMaterial = utility.GetMaterial(VanillaMaterial.Invalid);
		}

		if (highlightMaterial != null)
			destinationHighlighter.Highlight(position, direction, selectedCarBounds, highlightMaterial);
		else
			destinationHighlighter.TurnOff();
	}

	private static string ContentFromType(TrainCarType carType)
	{
		string name = Enum.GetName(typeof(TrainCarType), carType);
		GameObject? prefab = TransitionHelpers.ToV2(carType).prefab;
		TrainCar? trainCar = prefab?.GetComponent<TrainCar>();
		float? length = trainCar?.InterCouplerDistance;
		string lengthString = length.HasValue ? $"{length.Value}m" : "???";
		string addendum = Finance.CanAfford(carType) ? "" : "\n\nInsufficient funds.";
		return $"{name}\n{lengthString}{addendum}";
	}

	private static LCDArrowState GetArrowState(Transform signalOrigin, EquiPointSet.Point? spawnPoint, bool reverseDirection)
	{
		if (!spawnPoint.HasValue)
		{
			return LCDArrowState.Off;
		}
		bool isRight = 0f >= Mathf.Sin(
			0.0174532924f * Vector3.SignedAngle(
				reverseDirection ? (-spawnPoint.Value.forward) : spawnPoint.Value.forward,
				signalOrigin.forward,
				Vector3.up));
		return isRight ? LCDArrowState.Right : LCDArrowState.Left;
	}

	private static bool IsPlaceable(TrainCarType carType, [NotNullWhen(true)] RailTrack? track, [NotNullWhen(true)] EquiPointSet.Point? point)
	{
		return Finance.CanAfford(carType) && track != null && point.HasValue;
	}
}
