using System;
using CommsRadioAPI;
using DV;

namespace RollingStockOwnership.CommsRadio.EquipmentPurchaser;

internal class ErrorViewer : AStateBehaviour
{
	public ErrorViewer(string error) : base(
		new CommsRadioState(
			titleText: Main.Localize("comms_error_title"),
			contentText: error,
			actionText: Main.Localize("comms_error_action_positive"),
			buttonBehaviour: ButtonBehaviourType.Override
		)
	) {}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		switch (action)
		{
			case InputAction.Activate:
				utility.PlaySound(VanillaSoundCommsRadio.Cancel);
				return new MainMenu();

			case InputAction.Up:
			case InputAction.Down:
				return this;

			default:
				throw new Exception($"Unexpected action: {action}");
		}
	}
}
