using System;
using CommsRadioAPI;
using DV;

namespace DVOwnership.CommsRadio.EquipmentPurchaser;

internal class MainMenu : AStateBehaviour
{
	public MainMenu() : base(new CommsRadioState(titleText: "Rolling Stock", contentText: "Buy equipment?", buttonBehaviour: ButtonBehaviourType.Regular)) {}

	public override void OnEnter(CommsRadioUtility utility, AStateBehaviour? previous)
	{
		base.OnEnter(utility, previous);
		TrainCarTypePicker.UpdateAvailableCarTypes();
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		utility.PlaySound(VanillaSoundCommsRadio.ModeEnter);
		return action switch
		{
			InputAction.Activate => new TrainCarTypePicker(TrainCarTypePicker.LastIndex),
			_ => throw new Exception($"Unexpected action: {action}"),
		};
	}
}
