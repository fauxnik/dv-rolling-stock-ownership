using System;
using System.Collections.Generic;
using System.Linq;
using CommsRadioAPI;
using DV;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DVOwnership.Patches;

namespace DVOwnership.CommsRadio.EquipmentPurchaser;

internal class TrainCarTypePicker : AStateBehaviour
{
	public static int LastIndex = 0;
	private static List<TrainCarType> availableCarTypes = new List<TrainCarType>();

	private int selectedIndex;

	public TrainCarTypePicker(int selectedIndex) : base(
		new CommsRadioState(
			titleText: "Rolling Stock",
			contentText: ContentFromIndex(selectedIndex),
			actionText: Finance.CanAfford(availableCarTypes[selectedIndex]) ? "Buy" : "Cancel",
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
				if (Finance.CanAfford(availableCarTypes[selectedIndex]))
				{
					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
					// TODO: transition to destination picker
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
		string name = Enum.GetName(typeof(TrainCarType), type);
		float price = Finance.CalculateCarPrice(type);
		string addendum = Finance.CanAfford(price) ? "" : "\n\nInsufficient funds.";
		return $"{name}\n${price}{addendum}";
	}
}
