using System;
using CommsRadioAPI;
using DV;
using DV.Localization;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

internal class MainMenu : AStateBehaviour
{
	public MainMenu() : base(
		new CommsRadioState(
			titleText: LocalizationAPI.L("comms_mode_title"),
			contentText: LocalizationAPI.L("comms_mode_action"),
			buttonBehaviour: ButtonBehaviourType.Regular
		)
	) {}

	public override void OnEnter(CommsRadioUtility utility, AStateBehaviour? previous)
	{
		base.OnEnter(utility, previous);
		TrainCarLiveryPicker.UpdateAvailableCarTypes();
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		utility.PlaySound(VanillaSoundCommsRadio.ModeEnter);
		return action switch
		{
			InputAction.Activate => new TrainCarLiveryPicker(TrainCarLiveryPicker.LastIndex),
			_ => throw new Exception($"Unexpected action: {action}"),
		};
	}
}
