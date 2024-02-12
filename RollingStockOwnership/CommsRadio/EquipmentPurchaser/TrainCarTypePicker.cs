using System;
using System.Collections.Generic;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.Localization;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using RollingStockOwnership.Patches;
using UnityEngine;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

internal class TrainCarTypePicker : AStateBehaviour
{
	public static int LastIndex = 0;
	private static List<TrainCarType> availableCarTypes = new List<TrainCarType>();

	private int selectedIndex;

	public TrainCarTypePicker(int selectedIndex) : base(
		new CommsRadioState(
			titleText: LocalizationAPI.L("comms_mode_title"),
			contentText: ContentFromIndex(selectedIndex),
			actionText: Finance.CanAfford(availableCarTypes[selectedIndex])
				? LocalizationAPI.L("comms_car_type_action_positive")
				: LocalizationAPI.L("comms_car_type_action_negative"),
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
				TrainCarType selectedCarType = availableCarTypes[selectedIndex];
				GameObject? prefab = TransitionHelpers.ToV2(selectedCarType).prefab;
				TrainCar? trainCar = prefab?.GetComponent<TrainCar>();
				Bounds? carBounds = trainCar?.Bounds;
				if (Finance.CanAfford(selectedCarType))
				{
					if (!carBounds.HasValue) { throw new Exception($"Can't find car bounds for car type: {selectedCarType}"); }
					Main.LogDebug(() => $"Selected livery: {selectedCarType}");
					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
					return new DestinationPicker(selectedCarType, carBounds.Value, utility.SignalOrigin);
				}
				utility.PlaySound(VanillaSoundCommsRadio.Cancel);
				return new MainMenu();

			case InputAction.Up:
				return new TrainCarTypePicker(PreviousIndex());

			case InputAction.Down:
				return new TrainCarTypePicker(NextIndex());

			default:
				throw new Exception($"Unexpected action: {action}");
		};
	}

	private int NextIndex()
	{
		int nextIndex = selectedIndex + 1;
		if (nextIndex >= availableCarTypes.Count)
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
			previousIndex = availableCarTypes.Count - 1;
		}
		return previousIndex;
	}

	public static List<TrainCarType> UpdateAvailableCarTypes()
	{
		var previousLastCarType = availableCarTypes.Count > 0 ? availableCarTypes[LastIndex] : TrainCarType.NotSet;
		var allowedCarTypes = from carType in TrainCarTypeIntegrator.AllCarTypes
							  where !UnmanagedTrainCarTypes.UnmanagedTypes.Contains(carType)
							  select carType;
		var licensedCarTypes = from carType in allowedCarTypes
							   where CarTypes.IsAnyLocomotiveOrTender(TransitionHelpers.ToV2(carType))
								   ? LicenseManager_Patches.IsLicensedForLoco(TrainCarTypeIntegrator.LocoForTender(carType))
								   : LicenseManager_Patches.IsLicensedForCar(carType)
							   select carType;
		availableCarTypes = licensedCarTypes.ToList();
		LastIndex = availableCarTypes.FindIndex(carType => carType == previousLastCarType);
		if (LastIndex == -1) { LastIndex = 0; }
		return availableCarTypes;
	}

	private static string ContentFromIndex(int index)
	{
		TrainCarType type = availableCarTypes[index];
		string name = LocalizationAPI.L(type.ToV2().localizationKey);
		float price = Finance.CalculateCarPrice(type);
		string financeReport = Finance.CanAfford(price) ? "" : LocalizationAPI.L("comms_finance_error");
		return LocalizationAPI.L("comms_car_type_content", new string[] { name, price.ToString("N0"), financeReport });
	}
}
