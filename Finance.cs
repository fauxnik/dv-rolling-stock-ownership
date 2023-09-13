using DV;
using DV.InventorySystem;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;

namespace DVOwnership;

internal static class Finance
{
	internal static bool CanAfford(float price)
	{
		return SingletonBehaviour<Inventory>.Instance.PlayerMoney >= price;
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
		if (DVOwnership.Settings.isPriceScaledWithDifficulty) { price = ScalePriceBasedOnDifficulty(price, isLoco); }
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

	private static float ScalePriceBasedOnDifficulty(float price, bool isLoco)
	{
		return price;
		// return GamePreferences.Get<CareerDifficultyValues>(Preferences.CareerDifficulty) switch
		// {
		// 	CareerDifficultyValues.HARDCORE => Mathf.Pow(price / 10_000f, 1.1f) * 10_000f,
		// 	CareerDifficultyValues.CASUAL => price / (isLoco ? 100f : 10f),
		// 	_ => price,
		// };
	}
}
