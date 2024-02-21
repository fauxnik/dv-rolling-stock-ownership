using CommsRadioAPI;
using DV;
using UnityEngine;

namespace RollingStockOwnership.CommsRadio;

internal static class JobRequesterMode
{
	public static void Create()
	{
		CommsRadioMode.Create(
			new JobRequester.MainMenu(),
			new Color(0f, 0f, 0f, 0f) // Zero alpha hides the laser beam
		);
	}
}
