using System;
using System.Collections.Generic;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.Localization;
using DV.ThingTypes;
using RollingStockOwnership.Patches;
using UnityEngine;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

internal class TrainCarLiveryPicker : AStateBehaviour
{
	public static int LastIndex = 0;
	private static List<TrainCarLivery> availableCarLiveries = new List<TrainCarLivery>();

	private int selectedIndex;

	public TrainCarLiveryPicker(int selectedIndex) : base(
		new CommsRadioState(
			titleText: Main.Localize("comms_mode_title"),
			contentText: ContentFromIndex(selectedIndex),
			actionText: Finance.CanAfford(availableCarLiveries[selectedIndex])
				? Main.Localize("comms_car_type_action_positive")
				: Main.Localize("comms_car_type_action_negative"),
			buttonBehaviour: ButtonBehaviourType.Override
		)
	) {
		LastIndex = this.selectedIndex = selectedIndex;
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		switch(action)
		{
			case InputAction.Activate:
				TrainCarLivery selectedCarLivery = availableCarLiveries[selectedIndex];
				GameObject? prefab = selectedCarLivery.prefab;
				TrainCar? trainCar = prefab?.GetComponent<TrainCar>();
				Bounds? carBounds = trainCar?.Bounds;
				if (Finance.CanAfford(selectedCarLivery))
				{
					if (!carBounds.HasValue) { throw new Exception($"Can't find car bounds for car type: {selectedCarLivery.name}"); }
					Main.LogDebug(() => $"Selected livery: {selectedCarLivery.name}");
					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
					return new DestinationPicker(selectedCarLivery, carBounds.Value, utility.SignalOrigin);
				}
				utility.PlaySound(VanillaSoundCommsRadio.Cancel);
				return new MainMenu();

			case InputAction.Up:
				return new TrainCarLiveryPicker(PreviousIndex());

			case InputAction.Down:
				return new TrainCarLiveryPicker(NextIndex());

			default:
				throw new Exception($"Unexpected action: {action}");
		};
	}

	private int NextIndex()
	{
		int nextIndex = selectedIndex + 1;
		if (nextIndex >= availableCarLiveries.Count)
		{
			nextIndex = 0;
		}
		return nextIndex;
	}

	private int PreviousIndex()
	{
		int previousIndex = selectedIndex - 1;
		if (previousIndex < 0)
		{
			previousIndex = availableCarLiveries.Count - 1;
		}
		return previousIndex;
	}

	public static List<TrainCarLivery> UpdateAvailableCarTypes()
	{
		var previousLastCarType = availableCarLiveries.Count > 0 ? availableCarLiveries[LastIndex] : null;
		var allowedCarLiveries = from carLivery in TrainCarLiveryIntegrator.AllCarLiveries
							  where !UnmanagedTrainCarLiveries.UnmanagedLiveries.Contains(carLivery)
							  select carLivery;
		var licensedCarLiveries = from carLivery in allowedCarLiveries
							   where CarTypes.IsRegularCar(carLivery)
								   ? LicenseManager_Patches.IsLicensedForCar(carLivery)
								   : LicenseManager.Instance.IsLicensedForCar(TrainCarLiveryIntegrator.LocoForTender(carLivery))
							   select carLivery;
		availableCarLiveries = licensedCarLiveries.ToList();
		LastIndex = availableCarLiveries.FindIndex(carType => carType == previousLastCarType);
		if (LastIndex == -1) { LastIndex = 0; }
		return availableCarLiveries;
	}

	private static string ContentFromIndex(int index)
	{
		TrainCarLivery livery = availableCarLiveries[index];
		string name = LocalizationAPI.L(livery.localizationKey);
		float price = Finance.CalculateCarPrice(livery);
		string financeReport = Finance.CanAfford(price) ? "" : Main.Localize("comms_finance_error");
		return Main.Localize("comms_car_type_content", name, price.ToString("N0"), financeReport);
	}
}
