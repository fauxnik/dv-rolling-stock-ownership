using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.CabControls;
using DV.InventorySystem;
using DV.Localization;
using DV.Logic.Job;
using DV.Shops;
using DV.Teleporters;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.UIFramework;
using HarmonyLib;
using MessageBox;
using RollingStockOwnership.Extensions;
using UnityEngine;

namespace RollingStockOwnership;

internal static class StartingConditions
{
	private static readonly System.Random random = new System.Random();

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

	private static List<TrainCar> AcquireStarterEquipment(StarterChoices choices, Vector3 position)
	{
		Inventory inventory = Inventory.Instance;
		UnusedTrainCarDeleter unusedTrainCarDeleter = UnusedTrainCarDeleter.Instance;
		CarSpawner carSpawner = CarSpawner.Instance;
		TrainCarLivery starterLoco = choices.SelectedLocomotiveType.ToV2().parentType.liveries.ElementAtRandom(random);
		List<TrainCarLivery> starterWagonLiveries = choices.SelectedWagonType.ToV2().parentType.liveries;
		List<TrainCarLivery> starterLiveries = new List<TrainCarLivery>
		{
			starterLoco,
			starterWagonLiveries.ElementAtRandom(random),
			starterWagonLiveries.ElementAtRandom(random),
			starterWagonLiveries.ElementAtRandom(random)
		};

		LicenseManager.Instance.AcquireGeneralLicense(starterLoco.requiredLicense);
		if (CarTypes.IsSteamLocomotive(starterLoco))
		{
			EnsurePlayerHasRequiredItemsForSteamLoco();
		}

		RailTrack track = CarSpawner.GetTrackClosestTo(position, 0, out int nodeIndex);
		double startSpan = 5;
		bool flipConsist = false;
		bool randomOrientation = false;
		bool playerSpawnedCar = false;
		List<TrainCar> spawnedTrainCars =
			carSpawner.SpawnCarTypesOnTrackStrict(starterLiveries, track, true, true, startSpan, flipConsist, randomOrientation, playerSpawnedCar);

		IEnumerable<Equipment> spawnedEquipment = spawnedTrainCars.Select(Equipment.FromTrainCar);
		spawnedEquipment.Do(RollingStockManager.Instance.Add);

		return spawnedTrainCars;
	}

	private static IEnumerator ShowIntroductoryPopup(bool isShuntingLicenseChanged)
	{
		yield return WaitFor.SecondsRealtime(2);

		bool isWelcomeMessageShown = false;
		bool isTeleportTargetFound = false;
		bool isTeleporterFound = false;
		bool isLocoTypeSelected = false;
		bool isWagonTypeSelected = false;
		bool isStarterEquipmentAcquired = false;
		bool isPlayerTeleported = false;

		var choices = new StarterChoices();
		var locoOptions = new List<TrainCarType> { TrainCarType.LocoShunter, TrainCarType.LocoDM3, TrainCarType.LocoS060 };
		var wagonOptions = new List<TrainCarType>
		{
			TrainCarType.StockBrown, // single route, lowest average pay, lowest average tonnage, highest average pay to tonnage ratio
			TrainCarType.FlatbedStakes, // more routes, medium average pay, medium average tonnage, medium average pay to tonnage ratio
			TrainCarType.HopperTeal // most routes, highest average pay, highest average tonnage, lowest average pay to tonnage ratio
		};

		// Randomize the order of the displayed option
		locoOptions.Sort((_, _) => random.NextDouble() < 0.5 ? -1 : 1);
		wagonOptions.Sort((_, _) => random.NextDouble() < 0.5 ? -1 : 1);

		GameObject? teleportTarget = null;
		NonStationTeleporter? teleporter = null;

		PopupAPI.ShowOk(
			title: "Rolling Stock Ownership",
			message: Main.Localize("first_time_with_save"),
			positive: Main.Localize("first_time_with_save_positive")
		).Then((_) => {
			isWelcomeMessageShown = true;

			teleportTarget = GameObject.Find("TeleportHouse");
			if (teleportTarget == null) { throw new Exception("Failed to find player house teleport target"); }
			isTeleportTargetFound = true;

			teleporter = teleportTarget.GetComponent<NonStationTeleporter>();
			if (teleporter == null) { throw new Exception("Failed to find player house teleporter"); }
			isTeleporterFound = true;

			string message = isShuntingLicenseChanged ? Main.Localize("given_shunting_license_choose_starter_loco") : Main.Localize("choose_starter_loco");

			return PopupAPI.Show3Buttons(
				title: "Rolling Stock Ownership",
				message: message,
				positive: LocalizationAPI.L(locoOptions[0].ToV2().parentType.localizationKey),
				negative: LocalizationAPI.L(locoOptions[1].ToV2().parentType.localizationKey),
				abort: LocalizationAPI.L(locoOptions[2].ToV2().parentType.localizationKey)
			);
		}).Then((popupResult) => {
			PopupClosedByAction pressedButton = popupResult.closedBy;
			TrainCarType locoChoice = pressedButton == PopupClosedByAction.Positive
				? locoOptions[0]
				: pressedButton == PopupClosedByAction.Negative
					? locoOptions[1]
					: locoOptions[2];
			choices.ChooseLocomotive(locoChoice);
			isLocoTypeSelected = true;

			return PopupAPI.Show3Buttons(
				title: "Rolling Stock Ownership",
				message: Main.Localize("choose_starter_wagon"),
				positive: LocalizationAPI.L(wagonOptions[0].ToV2().parentType.localizationKey),
				negative: LocalizationAPI.L(wagonOptions[1].ToV2().parentType.localizationKey),
				abort: LocalizationAPI.L(wagonOptions[2].ToV2().parentType.localizationKey)
			);
		}).Then<List<TrainCar>>((popupResult) => {
			PopupClosedByAction pressedButton = popupResult.closedBy;
			TrainCarType wagonChoice = pressedButton == PopupClosedByAction.Positive
				? wagonOptions[0]
				: pressedButton == PopupClosedByAction.Negative
					? wagonOptions[1]
					: wagonOptions[2];
			choices.ChooseWagonType(wagonChoice);
			isWagonTypeSelected = true;

			if (teleporter == null) { throw new Exception("Player house teleporter unexpectedly null"); }
			List<TrainCar> spawnedTrainCars = AcquireStarterEquipment(choices, teleporter.playerTeleportAnchor.position);
			isStarterEquipmentAcquired = true;

			IEnumerator WaitForCoupling(Action proceed)
			{
				while (!spawnedTrainCars.All(trainCar => trainCar.frontCoupler.IsCoupled() || trainCar.rearCoupler.IsCoupled()))
				{
					yield return null;
				}
				proceed();
			}

			return new RSG.Promise<List<TrainCar>>((resolve, _) => {
				// The spawned equipment needs to be coupled before we delete it
				CoroutineManager.Instance.StartCoroutine(WaitForCoupling(() => resolve(spawnedTrainCars)));
			});
		}).Then((spawnedTrainCars) => {
			// The spawned equipment must be deleted/respawned to prevent a weird bug where the wagons become immovable
			spawnedTrainCars.Select(RollingStockManager.Instance.FindByTrainCar).Do(equipment => equipment.PrepareForDespawning());
			CarSpawner.Instance.DeleteTrainCars(spawnedTrainCars);

			return PopupAPI.ShowOk(
				title: "Rolling Stock Ownership",
				message: Main.Localize("teleport_to_starter_equipment", choices.LocalizedSelectedLocomotive, choices.LocalizedSelectedWagon),
				positive: Main.Localize("teleport_to_starter_equipment_positive")
			);
		}).Then((_) => {
			if (teleporter == null) { throw new Exception("Player house teleporter unexpectedly null"); }

			teleporter.TeleportToStation();
			isPlayerTeleported = true;

			// Restarting the spawn state manager causes the deleted equipment to respawn as soon as possible
			SpawnStateManager.Instance.Stop();
			SpawnStateManager.Instance.Start();
		}).Catch((exception) => {
			Main.LogError($"Exception thrown during RSO setup: {exception}");

			string errorReference = "unknown error";
			if (!isWelcomeMessageShown) { errorReference = "error displaying welcome message"; }
			else if (!isTeleportTargetFound) { errorReference = "error finding teleport target"; }
			else if (!isTeleporterFound) { errorReference = "error finding teleporter"; }
			else if (!isLocoTypeSelected) { errorReference = "error selecting loco type"; }
			else if (!isWagonTypeSelected) { errorReference = "error selecting wagon type"; }
			else if (!isStarterEquipmentAcquired) { errorReference = "error acquiring starter equipment"; }
			else if (!isPlayerTeleported) { errorReference = "error teleporting player"; }

			PopupAPI.ShowOk(
				title: "Rolling Stock Ownership",
				message: Main.Localize("starting_conditions_error", errorReference),
				positive: Main.Localize("starting_conditions_error_positive")
			);
		});
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

	private class StarterChoices
	{
		public TrainCarType SelectedLocomotiveType { get; private set; }
		public TrainCarType SelectedWagonType { get; private set; }

		public string LocalizedSelectedLocomotive
		{
			get { return LocalizationAPI.L(SelectedLocomotiveType.ToV2().parentType.localizationKey); }
		}
		public string LocalizedSelectedWagon
		{
			get { return LocalizationAPI.L(SelectedWagonType.ToV2().parentType.localizationKey); }
		}

		public void ChooseLocomotive(TrainCarType locomotiveType)
		{
			SelectedLocomotiveType = locomotiveType;
		}

		public void ChooseWagonType(TrainCarType wagonType)
		{
			SelectedWagonType = wagonType;
		}
	}
}
