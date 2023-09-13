using System;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.InventorySystem;
using DV.PointSet;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using DVOwnership.Extensions;
using UnityEngine;

namespace DVOwnership.CommsRadio.EquipmentPurchaser;

internal class PurchaseConfirmer : AStateBehaviour
{
	private TrainCarType selectedCarType;
	private RailTrack destinationTrack;
	private EquiPointSet.Point selectedSpawnPoint;
	private bool isSelectedOrientationOppositeTrackDirection;
	private CarDestinationHighlighter destinationHighlighter;
	private bool confirmPurchase;

	private Vector3 SelectedPosition { get => (Vector3)selectedSpawnPoint.position + WorldMover.currentMove; }
	private Vector3 SelectedDirection { get => selectedSpawnPoint.forward; }

	public PurchaseConfirmer(
		TrainCarType carType,
		RailTrack track,
		EquiPointSet.Point spawnPoint,
		bool reverseDirection,
		CarDestinationHighlighter highlighter,
		bool buy = true
	) : base(
		new CommsRadioState(
			titleText: "Rolling Stock",
			contentText: ContentFromType(carType),
			actionText: buy ? "confirm" : "cancel",
			buttonBehaviour: ButtonBehaviourType.Override
		)
	) {
		selectedCarType = carType;
		destinationTrack = track;
		selectedSpawnPoint = spawnPoint;
		isSelectedOrientationOppositeTrackDirection = reverseDirection;
		destinationHighlighter = highlighter;
		confirmPurchase = buy;
	}

	public override void OnEnter(CommsRadioUtility utility)
	{
		base.OnEnter(utility);
		destinationHighlighter.TurnOn();
	}

	public override void OnLeave(CommsRadioUtility utility)
	{
		base.OnLeave(utility);
		destinationHighlighter.TurnOff();
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		switch (action)
		{
			case InputAction.Activate:
				if (confirmPurchase && Finance.CanAfford(selectedCarType))
				{
					DVOwnership.Log($"Spawning {selectedCarType} on track {destinationTrack.logicTrack.ID.FullID}");

					Vector3 position = SelectedPosition;
					Vector3 direction = SelectedDirection;
					if (isSelectedOrientationOppositeTrackDirection) { direction *= -1f; }

					GameObject? prefab = TransitionHelpers.ToV2(selectedCarType).prefab;
					if (prefab == null)
					{
						DVOwnership.LogError($"Couldn't find prefab for {selectedCarType}");
						return new ErrorViewer("Unable to manufacture requested equipment.");
					}

					TrainCar trainCar = CarSpawner.Instance.SpawnCar(prefab, destinationTrack, position, direction);
					if (trainCar == null)
					{
						DVOwnership.LogError($"CarSpawner didn't return a TrainCar for {selectedCarType}");
						return new ErrorViewer("Unable to deliver requested equipment.");
					}

					if (!trainCar.trainset.cars.Any(car => car.brakeSystem.brakeset.anyHandbrakeApplied))
					{
						trainCar.brakeSystem.handbrakePosition = 1f;
					}

					Inventory.Instance.RemoveMoney(Finance.CalculateCarPrice(selectedCarType));
					utility.PlaySound(VanillaSoundCommsRadio.MoneyRemoved);

					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
					utility.PlayVehicleSound(VanillaSoundVehicle.SpawnVehicle, trainCar);
					SingletonBehaviour<RollingStockManager>.Instance.Add(Equipment.FromTrainCar(trainCar));
				}
				else
				{
					utility.PlaySound(VanillaSoundCommsRadio.Cancel);
				}
				return new MainMenu();

			case InputAction.Up:
			case InputAction.Down:
				return new PurchaseConfirmer(
					selectedCarType,
					destinationTrack,
					selectedSpawnPoint,
					isSelectedOrientationOppositeTrackDirection,
					destinationHighlighter,
					!confirmPurchase
				);

			default:
				throw new Exception($"Unexpected action: {action}");
		}
	}

	private static string ContentFromType(TrainCarType carType)
	{
		string name = Enum.GetName(typeof(TrainCarType), carType);
		float price = Finance.CalculateCarPrice(carType);
		string addendum = Finance.CanAfford(price) ? "" : "\n\nInsufficient funds.";
		return $"Buy {name} for ${price}?{addendum}";
	}
}
