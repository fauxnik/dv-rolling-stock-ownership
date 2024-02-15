using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.CabControls;
using DV.InventorySystem;
using DV.Localization;
using DV.Logic.Job;
using DV.Shops;
using DV.Simulation.Cars;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using MessageBox;
using UnityEngine;

namespace RollingStockOwnership;

internal static class StartingConditions
{
	private static readonly Vector3 starterLocoSpawnPosition = new Vector3(
		5698.63135f,
		155.929153f,
		7622.781f
	);
	private static readonly TrainCarLivery[] startingWagons = {
		TrainCarType.FlatbedStakes.ToV2(),
		TrainCarType.FlatbedStakes.ToV2(),
		TrainCarType.FlatbedStakes.ToV2(),
	};

	internal static void Verify()
	{
		bool isShuntingLicenseChanged = false;
		LicenseManager licenseManager = LicenseManager.Instance;
		if (!licenseManager.IsJobLicenseAcquired(TransitionHelpers.ToV2(JobLicenses.Shunting)))
		{
			licenseManager.AcquireJobLicense(TransitionHelpers.ToV2(JobLicenses.Shunting));
			isShuntingLicenseChanged = true;
		}

		if (RollingStockManager.Instance.AllEquipment.Count == 0)
		{
			ClearWorldJobs();
			ClearWorldTrains();
			CoroutineManager.Instance.StartCoroutine(AcquireStarterEquipment());
			CoroutineManager.Instance.StartCoroutine(ShowIntroductoryPopup(isShuntingLicenseChanged));
		}
	}

	private static void ClearWorldJobs()
	{
		JobsManager.Instance.AbandonAllJobs();
		StationController.allStations.ForEach(station => {
			station.logicStation.ExpireAllAvailableJobsInStation();
			object maybeSpawnedJobOverviews = AccessTools.Field(typeof(StationController), "spawnedJobOverviews").GetValue(station);
			if (maybeSpawnedJobOverviews is List<JobOverview> spawnedJobOverviews)
			{
				while (spawnedJobOverviews.Count > 0)
				{
					JobOverview jobOverview = spawnedJobOverviews[0];
					spawnedJobOverviews.Remove(jobOverview);
					jobOverview.DestroyJobOverview();
				}
			}
		});
	}

	private static void ClearWorldTrains()
	{
		CarSpawner.Instance.DeleteTrainCars(CarSpawner.Instance.AllCars, true);
	}

	private static IEnumerator AcquireStarterEquipment()
	{
		yield return new WaitForSeconds(1);

		Inventory inventory = Inventory.Instance;
		UnusedTrainCarDeleter unusedTrainCarDeleter = UnusedTrainCarDeleter.Instance;
		CarSpawner carSpawner = CarSpawner.Instance;
		TrainCarLivery starterLoco = ((TrainCarType) Main.Settings.starterLocoType).ToV2();

		LicenseManager.Instance.AcquireGeneralLicense(starterLoco.requiredLicense);
		if (CarTypes.IsSteamLocomotive(starterLoco))
		{
			EnsurePlayerHasRequiredItemsForSteamLoco();
		}

		bool flipRotation = false;
		bool playerSpawnedCar = false;
		bool uniqueCar = false;
		IEnumerable<Equipment> spawnedEquipment =
			new List<TrainCar>
			{
				carSpawner.SpawnCarOnClosestTrack(
					starterLocoSpawnPosition + WorldMover.currentMove,
					starterLoco,
					flipRotation,
					playerSpawnedCar,
					uniqueCar
				)
			}
			.Select(Equipment.FromTrainCar);

		foreach (Equipment equipment in spawnedEquipment)
		{
			yield return null;

			RollingStockManager.Instance.Add(equipment);

			SimController? simController = equipment.GetTrainCar()?.GetComponent<SimController>();
			BaseControlsOverrider? baseControlsOverrider = simController?.controlsOverrider;

			if (baseControlsOverrider != null)
			{
				BaseControlsOverrider.SpawnType brakesOnSpawn =
					equipment.IsCoupledFront || equipment.IsCoupledRear
					? BaseControlsOverrider.SpawnType.GAME_LOAD_COUPLED
					: BaseControlsOverrider.SpawnType.GAME_LOAD_UNCOUPLED;

				baseControlsOverrider.SetBrakesOnSpawn(brakesOnSpawn);
			}
		}

		foreach(TrainCarLivery livery in startingWagons)
		{
			inventory.AddMoney(Finance.CalculateCarPrice(livery));
		}
	}

	private static IEnumerator ShowIntroductoryPopup(bool isShuntingLicenseChanged)
	{
		yield return new WaitForSeconds(2);

		string locoName = LocalizationAPI.L(((TrainCarType) Main.Settings.starterLocoType).ToV2().localizationKey);

		PopupAPI.ShowOk(
			title: "Rolling Stock Ownership",
			message: LocalizationAPI.L("first_time_with_save"),
			positive: LocalizationAPI.L("first_time_with_save_positive")
		).Then((_) => (
			PopupAPI.ShowOk(
				title: "Rolling Stock Ownership",
				message: string.Join(" ", new string [] {
					isShuntingLicenseChanged ? LocalizationAPI.L("given_shunting_license") : "",
					LocalizationAPI.L("given_starter_equipment", new string [] { locoName }),
				}),
				positive: LocalizationAPI.L("given_starter_equipment_positive")
			)
		));
	}

	private static void EnsurePlayerHasRequiredItemsForSteamLoco()
	{
		List<ShopItemData> shopItemsData = GlobalShopController.Instance.shopItemsData;
		Main.LogDebug(() => $"All shop items:\n{{\n\t{string.Join(",\n\t", shopItemsData.Select(itemData => itemData.item.name))}\n}}");

		string [] itemNames = { "shovel", "lighter" };
		foreach (string itemName in itemNames)
		{
			bool isItemInInventory = Inventory.Instance.GetItemByName(itemName, false) != null;
			bool isItemInAnyStorage = StorageController.Instance.allStorages.Any(StorageContainsItemByName(itemName));
			if (isItemInInventory || isItemInAnyStorage)
			{
				Main.Log($"Found {itemName} in player inventory or storage, skipping.");
				continue;
			}

			ShopItemData data = shopItemsData.Find(data => data?.item?.name?.ToLower() == itemName.ToLower());
			if (data == null)
			{
				Main.LogError($"Can't find {itemName} in global shop items data! Did its name change?");
				continue;
			}

			if (data.item.gameObject == null)
			{
				Main.LogError($"The game object for {itemName} is null! Not adding to inventory.");
				continue;
			}

			GameObject instantiatedItem = GameObject.Instantiate(data.item.gameObject);
			if (Inventory.Instance.AddItemToInventory(instantiatedItem) >= 0) { continue; }

			Main.LogWarning($"Couldn't add {itemName} to inventory. Adding to lost and found instead.");
			StorageController.Instance.AddItemToStorageItemList(StorageController.Instance.StorageLostAndFound, instantiatedItem);
		}
	}

	private static Func<StorageBase, bool> StorageContainsItemByName(string itemName)
	{
		return (StorageBase storage) => {
			try
			{
				// StorageBase doesn't offer a way of querying for contents by name.
				List<ItemBase> items = (List<ItemBase>) AccessTools.Field(typeof(StorageBase), "itemList").GetValue(storage);
				return items.Any(item => item.name.ToLower() == itemName.ToLower());
			}
			catch (Exception exception)
			{
				Main.LogError($"Exception thrown while determining if {storage.name} contains {itemName}! Returning false.\n{exception}");
				return false;
			}
		};
	}
}
