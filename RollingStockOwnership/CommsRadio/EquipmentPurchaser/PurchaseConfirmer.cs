using System;
using System.Collections;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.InventorySystem;
using DV.Localization;
using DV.PointSet;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using RollingStockOwnership.Extensions;
using UnityEngine;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

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
			titleText: LocalizationAPI.L("comms_mode_title"),
			contentText: ContentFromType(carType),
			actionText: buy
				? LocalizationAPI.L("comms_confirmation_action_positive")
				: LocalizationAPI.L("comms_confirmation_action_negative"),
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

	public override void OnEnter(CommsRadioUtility utility, AStateBehaviour? previous)
	{
		base.OnEnter(utility, previous);
		if (!(previous is PurchaseConfirmer))
			destinationHighlighter.TurnOn();
	}

	public override void OnLeave(CommsRadioUtility utility, AStateBehaviour? next)
	{
		base.OnLeave(utility, next);
		if (!(next is PurchaseConfirmer))
			destinationHighlighter.TurnOff();
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		switch (action)
		{
			case InputAction.Activate:
				if (confirmPurchase && Finance.CanAfford(selectedCarType))
				{
					Main.Log($"Spawning {selectedCarType} on track {destinationTrack.logicTrack.ID.FullID}");

					Vector3 position = SelectedPosition;
					Vector3 direction = SelectedDirection;
					if (isSelectedOrientationOppositeTrackDirection) { direction *= -1f; }

					GameObject? prefab = TransitionHelpers.ToV2(selectedCarType).prefab;
					if (prefab == null)
					{
						Main.LogError($"Couldn't find prefab for {selectedCarType}");
						return new ErrorViewer("Unable to manufacture requested equipment.");
					}

					TrainCar trainCar = CarSpawner.Instance.SpawnCar(prefab, destinationTrack, position, direction);
					if (trainCar == null)
					{
						Main.LogError($"CarSpawner didn't return a TrainCar for {selectedCarType}");
						return new ErrorViewer("Unable to deliver requested equipment.");
					}

					utility.StartCoroutine(CheckHandbrakeNextFrame(trainCar));

					Inventory.Instance.RemoveMoney(Finance.CalculateCarPrice(selectedCarType));
					utility.PlaySound(VanillaSoundCommsRadio.MoneyRemoved);

					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
					utility.PlayVehicleSound(VanillaSoundVehicle.SpawnVehicle, trainCar);
					RollingStockManager.Instance.Add(Equipment.FromTrainCar(trainCar));
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
		string name = LocalizationAPI.L(carType.ToV2().localizationKey);
		float price = Finance.CalculateCarPrice(carType);
		string financeReport = Finance.CanAfford(price) ? "" : LocalizationAPI.L("comms_finance_error");
		return LocalizationAPI.L("comms_confirmation_content", new string[] { name, price.ToString("N0"), financeReport });
	}

	private static IEnumerator CheckHandbrakeNextFrame(TrainCar trainCar)
	{
		yield return null;
		if (!trainCar.trainset.cars.Any(car => car.brakeSystem.brakeset.anyHandbrakeApplied))
		{
			trainCar.brakeSystem.handbrakePosition = 1f;
		}
	}
}
