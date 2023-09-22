using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.InventorySystem;
using DV.Simulation.Cars;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using UnityEngine;

namespace DVOwnership;

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
		LicenseManager licenseManager = SingletonBehaviour<LicenseManager>.Instance;
		if (!licenseManager.IsJobLicenseAcquired(TransitionHelpers.ToV2(JobLicenses.Shunting)))
		{
			licenseManager.AcquireJobLicense(TransitionHelpers.ToV2(JobLicenses.Shunting));
			isShuntingLicenseChanged = true;
		}

		RollingStockManager rollingStockManager = SingletonBehaviour<RollingStockManager>.Instance;
		if (rollingStockManager.AllEquipment.Count == 0)
		{
			CoroutineManager.Instance.StartCoroutine(AcquireStarterEquipment(rollingStockManager));
			CoroutineManager.Instance.StartCoroutine(ShowIntroductoryPopup(isShuntingLicenseChanged));
		}
	}

	private static IEnumerator AcquireStarterEquipment(RollingStockManager rollingStockManager)
	{
		List<RailTrack> allTracks = new List<RailTrack>(SingletonBehaviour<RailTrackRegistry>.Instance.AllTracks);
		RailTrack playerHomeTrack = allTracks.Find(track => track.logicTrack.ID.FullID == playerHomeTrackID);

		Inventory inventory = SingletonBehaviour<Inventory>.Instance;
		UnusedTrainCarDeleter unusedTrainCarDeleter = SingletonBehaviour<UnusedTrainCarDeleter>.Instance;
		CarSpawner carSpawner = SingletonBehaviour<CarSpawner>.Instance;
		TrainCarLivery starterLoco = TrainCarType.LocoShunter.ToV2();
		IEnumerable<Equipment> roster = carSpawner.SpawnCarTypesOnTrackRandomOrientation(new List<TrainCarLivery> { starterLoco }, playerHomeTrack, true, true)
			.Select(Equipment.FromTrainCar);

		foreach (Equipment equipment in roster)
		{
			yield return null;

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
			inventory.AddMoney(CommsRadioEquipmentPurchaser.CalculateCarPrice(livery.v1));
		}
	}

	private static IEnumerator ShowIntroductoryPopup(bool isShuntingLicenseChanged)
	{
		yield return new WaitForSeconds(2);

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
					"A starter DE2 has been delivered to the player home, and a starting bonus has been deposited in your wallet.",
					"Use the cash to buy a few wagons. (You won't be able to make money without them.)"
				}),
				positive: "Let's go!"
			)
		);
	}
}
