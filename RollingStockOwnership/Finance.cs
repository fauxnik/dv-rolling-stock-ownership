using DV;
using DV.InventorySystem;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.UserManagement;

namespace RollingStockOwnership;

internal static class Finance
{
	internal static bool CanAfford(float price)
	{
		return Inventory.Instance.PlayerMoney >= price;
	}

	internal static bool CanAfford(TrainCarLivery carLivery)
	{
		return CanAfford(CalculateCarPrice(carLivery));
	}

	internal static float CalculateCarPrice(TrainCarLivery carLivery)
	{
		var isLoco = CarTypes.IsLocomotive(carLivery);
		var price = ResourceTypes.GetFullUnitPriceOfResource(ResourceType.Car_DMG, carLivery, gameParams: Globals.G.GameParams.ResourcesParams);
		price *= 10f; // Cost per unit damage appears to have been scaled down as of the Simulator update
		if (isLoco) { price = ScaleLocoPrice(price); }
		price = ScalePriceBasedOnGameMode(price);
#if DEBUG
		return 0;
#else
		return UnityEngine.Mathf.Round(price);
#endif
	}

	private static float ScaleLocoPrice(float price)
	{
		return price * 10f;
	}

	private static float ScalePriceBasedOnGameMode(float price)
	{
		if (UserManager.Instance.CurrentUser.CurrentSession.GameMode.Equals("FreeRoam"))
		{
			return price * Main.Settings.sandboxPriceMultiplier;
		}
		return price;
	}
}
