using System;
using System.Collections;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.InventorySystem;
using DV.Localization;
using DV.PointSet;
using DV.ThingTypes;
using RollingStockOwnership.Extensions;
using UnityEngine;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

internal class PurchaseConfirmer : AStateBehaviour
{
	private TrainCarLivery selectedCarLivery;
	private RailTrack destinationTrack;
	private EquiPointSet.Point selectedSpawnPoint;
	private bool isSelectedOrientationOppositeTrackDirection;
	private CarDestinationHighlighter destinationHighlighter;
	private bool confirmPurchase;

	private Vector3 SelectedPosition { get => (Vector3)selectedSpawnPoint.position + WorldMover.currentMove; }
	private Vector3 SelectedDirection { get => selectedSpawnPoint.forward; }

	public PurchaseConfirmer(
		TrainCarLivery carLivery,
		RailTrack track,
		EquiPointSet.Point spawnPoint,
		bool reverseDirection,
		CarDestinationHighlighter highlighter,
		bool buy = true
	) : base(
		new CommsRadioState(
			titleText: Main.Localize("comms_mode_title"),
			contentText: ContentFromType(carLivery),
			actionText: buy
				? Main.Localize("comms_confirmation_action_positive")
				: Main.Localize("comms_confirmation_action_negative"),
			buttonBehaviour: ButtonBehaviourType.Override
		)
	) {
		selectedCarLivery = carLivery;
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
				if (confirmPurchase && Finance.CanAfford(selectedCarLivery))
				{
					Main.Log($"Spawning {selectedCarLivery} on track {destinationTrack.logicTrack.ID.FullID}");

					Vector3 position = SelectedPosition;
					Vector3 direction = SelectedDirection;
					if (isSelectedOrientationOppositeTrackDirection) { direction *= -1f; }

					GameObject? prefab = selectedCarLivery.prefab;
					if (prefab == null)
					{
						Main.LogError($"Couldn't find prefab for {selectedCarLivery}");
						return new ErrorViewer("Unable to manufacture requested equipment.");
					}

					TrainCar trainCar = CarSpawner.Instance.SpawnCar(prefab, destinationTrack, position, direction);
					if (trainCar == null)
					{
						Main.LogError($"CarSpawner didn't return a TrainCar for {selectedCarLivery}");
						return new ErrorViewer("Unable to deliver requested equipment.");
					}

					utility.StartCoroutine(CheckHandbrakeNextFrame(trainCar));

					Inventory.Instance.RemoveMoney(Finance.CalculateCarPrice(selectedCarLivery));
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
					selectedCarLivery,
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

	private static string ContentFromType(TrainCarLivery carLivery)
	{
		string name = LocalizationAPI.L(carLivery.localizationKey);
		float price = Finance.CalculateCarPrice(carLivery);
		string financeReport = Finance.CanAfford(price) ? "" : Main.Localize("comms_finance_error");
		return Main.Localize("comms_confirmation_content", name, price.ToString("N0"), financeReport);
	}

	private static IEnumerator CheckHandbrakeNextFrame(TrainCar trainCar)
	{
		yield return null;
		if (trainCar.brakeSystem.hasHandbrake && !trainCar.trainset.cars.Any(car => car.brakeSystem.brakeset.anyHandbrakeApplied))
		{
			trainCar.brakeSystem.handbrakePosition = 1f;
		}
	}
}
