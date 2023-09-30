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

	internal static bool CanAfford(TrainCarType carType)
	{
		return CanAfford(CalculateCarPrice(carType));
	}

	internal static float CalculateCarPrice(TrainCarType carType)
	{
		var isLoco = CarTypes.IsLocomotive(TransitionHelpers.ToV2(carType));
		var price = ResourceTypes.GetFullUnitPriceOfResource(ResourceType.Car_DMG, TransitionHelpers.ToV2(carType), gameParams: Globals.G.GameParams.ResourcesParams);
		if (isLoco) { price = ScaleLocoPrice(price); }
		price = ScalePriceBasedOnGameMode(price);
#if DEBUG
		return 0;
#else
		return Mathf.Round(price);
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
