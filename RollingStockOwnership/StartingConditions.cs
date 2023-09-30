using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.InventorySystem;
using DV.Localization;
using DV.Logic.Job;
using DV.Simulation.Cars;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using UnityEngine;

namespace RollingStockOwnership;

internal static class StartingConditions
{
	private static readonly string playerHomeTrackID = "#Y-#S-392-#T";
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

		List<RailTrack> allTracks = new List<RailTrack>(RailTrackRegistry.Instance.AllTracks);
		RailTrack playerHomeTrack = allTracks.Find(track => track.logicTrack.ID.FullID == playerHomeTrackID);

		Inventory inventory = Inventory.Instance;
		UnusedTrainCarDeleter unusedTrainCarDeleter = UnusedTrainCarDeleter.Instance;
		CarSpawner carSpawner = CarSpawner.Instance;
		TrainCarType starterLoco = (TrainCarType) Main.Settings.starterLocoType;

		if (!LicenseManager.Instance.IsLicensedForCar(starterLoco.ToV2()))
		{
			LicenseManager.Instance.AcquireGeneralLicense(
				starterLoco switch
				{
					TrainCarType.LocoS060 => GeneralLicenseType.S060.ToV2(),
					TrainCarType.LocoDM3 => GeneralLicenseType.DM3.ToV2(),
					_ => GeneralLicenseType.DE2.ToV2(),
				}
			);
		}

		IEnumerable<Equipment> spawnedEquipment = carSpawner.SpawnCarTypesOnTrackRandomOrientation(new List<TrainCarLivery> { starterLoco.ToV2() }, playerHomeTrack, true, true)
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
			inventory.AddMoney(Finance.CalculateCarPrice(livery.v1));
		}
	}

	private static IEnumerator ShowIntroductoryPopup(bool isShuntingLicenseChanged)
	{
		yield return new WaitForSeconds(2);

		string locoName = LocalizationAPI.L(((TrainCarType) Main.Settings.starterLocoType).ToV2().localizationKey);

		MessageBox.ShowPopupOk(
			title: "Rolling Stock Ownership",
			message: string.Join(" ", new string[] {
				"It looks like this is the first time Rolling Stock Ownership has been used with this save.",
				"You'll find that all locomotives and wagons have been removed from the world,",
				"and that they don't magically appear at stations anymore.",
				"They must instead be purchased.",
				"A new mode has been added to the Comms Radio for this purpose."
			}),
			positive: "Understood",
			onClose: (_) => MessageBox.ShowPopupOk(
				title: "Rolling Stock Ownership",
				message: string.Join(" ", new string [] {
					isShuntingLicenseChanged ? "Since you'll need the shunting license to begin any jobs, it's been given to you." : "",
					$"A starter {locoName} has been delivered outside the player home, and a starting bonus has been deposited in your wallet.",
					"Use the cash to buy a few wagons. Choose wisely: you won't be able to make money without them!"
				}),
				positive: "Let's go!"
			)
		);
	}
}
