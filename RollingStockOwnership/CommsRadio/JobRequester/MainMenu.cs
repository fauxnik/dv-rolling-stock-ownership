using CommsRadioAPI;
using DV;
using System;

namespace RollingStockOwnership.CommsRadio.JobRequester;

internal class MainMenu : AStateBehaviour
{
	public MainMenu(bool cancel = false) : base(
		new CommsRadioState(
			titleText: Main.Localize("comms_job_mode_title"),
			contentText: Main.Localize("comms_job_mode_action"),
			buttonBehaviour: ButtonBehaviourType.Regular
		)
	) {}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		utility.PlaySound(VanillaSoundCommsRadio.ModeEnter);
		return action switch
		{
			InputAction.Activate => new GenerationConfirmer(),
			_ => throw new Exception($"Unexpected action: {action}"),
		};
	}
}
