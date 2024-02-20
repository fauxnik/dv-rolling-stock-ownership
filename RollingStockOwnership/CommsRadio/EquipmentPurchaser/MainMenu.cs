using System;
using CommsRadioAPI;
using DV;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

internal class MainMenu : AStateBehaviour
{
	public MainMenu() : base(
		new CommsRadioState(
			titleText: Main.Localize("comms_mode_title"),
			contentText: Main.Localize("comms_mode_action"),
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
