using System.Collections.Generic;
using System.Linq;
using DV.InventorySystem;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;

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
		LicenseManager licenseManager = SingletonBehaviour<LicenseManager>.Instance;
		if (!licenseManager.IsJobLicenseAcquired(TransitionHelpers.ToV2(JobLicenses.Shunting)))
		{
			licenseManager.AcquireJobLicense(TransitionHelpers.ToV2(JobLicenses.Shunting));
		}

		RollingStockManager rollingStockManager = SingletonBehaviour<RollingStockManager>.Instance;
		if (rollingStockManager.AllEquipment.Count == 0)
		{
			List<RailTrack> allTracks = new List<RailTrack>(SingletonBehaviour<RailTrackRegistry>.Instance.AllTracks);
			RailTrack playerHomeTrack = allTracks.Find(track => track.logicTrack.ID.FullID == playerHomeTrackID);

			Inventory inventory = SingletonBehaviour<Inventory>.Instance;
			UnusedTrainCarDeleter unusedTrainCarDeleter = SingletonBehaviour<UnusedTrainCarDeleter>.Instance;
			CarSpawner carSpawner = SingletonBehaviour<CarSpawner>.Instance;
			TrainCarLivery starterLoco = TrainCarType.LocoShunter.ToV2();
			carSpawner.SpawnCarTypesOnTrackRandomOrientation(new List<TrainCarLivery> { starterLoco }, playerHomeTrack, true, true)
				.ForEach(car => {
					rollingStockManager.Add(Equipment.FromTrainCar(car));
					if (!car.trainset.cars.Any(car => car.brakeSystem.brakeset.anyHandbrakeApplied))
					{
						car.brakeSystem.handbrakePosition = 1f;
					}
				});

			foreach(TrainCarLivery livery in startingWagons)
			{
				inventory.AddMoney(Finance.CalculateCarPrice(livery.v1));
			}

			MessageBox.ShowPopupOk(
				title: "Rolling Stock Ownership",
				message: string.Join(" ", new string[] {
					"It looks like this is the first time RSO has been used with this save.",
					"If you didn't already own it, the shunting license has been given to you.",
					"You will also find a starter locomotive outside the player home and some extra cash in your wallet.",
					"Use the extra cash to buy a few wagons as they won't magically appear at stations anymore.",
					"A purchase mode has been added to your Comms Radio for this purpose.",
					"\n\nGood luck and have fun!"
				})
			);
		}
	}
}
