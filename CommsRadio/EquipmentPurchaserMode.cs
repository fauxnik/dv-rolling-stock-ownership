using CommsRadioAPI;
using DV;
using UnityEngine;

namespace DVOwnership.CommsRadio;

internal static class EquipmentPurchaserMode
{
	public static void Create()
	{
		CommsRadioMode.Create(new EquipmentPurchaser.MainMenu(), new Color(1f, 0f, 0.9f, 1f), (mode) => mode is CommsRadioCarSpawner);
	}
}
