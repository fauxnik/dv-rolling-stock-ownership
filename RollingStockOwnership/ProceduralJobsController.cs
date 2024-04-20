using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using RollingStockOwnership.Extensions;
using RollingStockOwnership.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership;

public class ProceduralJobsController
{
	private static readonly int MAX_JOB_GENERATION_ATTEMPTS = 30;
	private static Dictionary<StationController, ProceduralJobsController> instances = new Dictionary<StationController, ProceduralJobsController>();

	private StationController stationController;
	private List<Track> stationTracks;
	private StationJobGenerationRange? stationRange;

	public static ProceduralJobsController ForStation(StationController stationController)
	{
		ProceduralJobsController instance;
		if (!instances.TryGetValue(stationController, out instance))
		{
			instance = new ProceduralJobsController(stationController);
			instances.Add(stationController, instance);
		}

		return instance;
	}

	private ProceduralJobsController(StationController stationController)
	{
		this.stationController = stationController;
		stationTracks = GetTracksByYardID(stationController.stationInfo.YardID).ToList();

		if (AccessTools.Field(typeof(StationController), "stationRange").GetValue(stationController) is StationJobGenerationRange range)
		{
			stationRange = range;
		}
		else
		{
			Main.LogError($"Can't find StationJobGenerationRange for {stationController.logicStation.ID}");
		}
	}

	public IEnumerator GenerateJobsCoro(Action onComplete, IEnumerable<Car>? carsToUse = null)
	{
		int tickCount = Environment.TickCount;
		Random rng = new Random(tickCount);
		var yardId = stationController.stationInfo.YardID;
		var proceduralRuleset = stationController.proceduralJobsRuleset;
		var manager = RollingStockManager.Instance;

		Main.LogDebug(() => $"Player owns licenses: [{string.Join(", ", LicenseManager.Instance.GetAcquiredJobLicenses().Union<Thing_v2>(LicenseManager.Instance.GetGeneralAcquiredLicenses()).Select(license => license.name))}]");

		lock (RollingStockManager.syncLock)
		{
			HashSet<Car> carsInYard;
			if (carsToUse != null)
			{
				carsInYard = carsToUse.ToHashSet();
			}
			else
			{
				// Get all cars in the yard
				carsInYard = new HashSet<Car>();
				if (stationRange != null)
				{
					foreach (var equipment in manager.AllEquipment)
					{
						yield return null;

						// check distance to station
						if (equipment.SquaredDistanceFromStation(stationController) > stationRange.generateJobsSqrDistance)
						{
							Main.LogDebug(() => $"Not using {equipment.ID} for job generation because it's outside of range");
							continue;
						}

						// respawn equipment or else GetTrainCar may return null
						if (!equipment.IsSpawned) { equipment.Spawn(); }
						TrainCar? wagon = equipment.GetTrainCar();

						if (wagon == null) {
							Main.LogWarning($"Unexpected null TrainCar! Not using {equipment.ID} for job generation");
							continue;
						}

						if (wagon.derailed)
						{
							Main.Log($"Not using {equipment.ID} for job generation because it's derailed");
							continue;
						}

						carsInYard.Add(wagon.logicCar);
					}
				}
				else
				{
					foreach (var track in stationTracks)
					{
						yield return null;

						// respawn equipment on track
						foreach (var equipment in manager.GetEquipmentOnTrack(track, false))
						{
							yield return null;
							if (!equipment.IsSpawned) { equipment.Spawn(); }
						}

						foreach (var car in track.GetCarsFullyOnTrack())
						{
							carsInYard.Add(car);
						}

						foreach (var car in track.GetCarsPartiallyOnTrack())
						{
							carsInYard.Add(car);
						}
					}
				}

				// Get all cars from player's train
				var playerTrainCars = PlayerManager.Car?.trainset?.cars ?? new List<TrainCar>();
				var playerCars = from trainCar in playerTrainCars select trainCar.logicCar;
				foreach (var car in playerCars) { carsInYard.Add(car); }
			}

			Main.LogDebug(() => $"Found {carsInYard.Count()} cars in {yardId} yard and player's train.");

			// Get all cars with assigned jobs
			var carsWithJobs = new HashSet<Car>();
			JobsManager jobsManager = JobsManager.Instance;
			var allJobs = (List<Job>) AccessTools.Field(typeof(JobsManager), "allJobs").GetValue(jobsManager);
			if (allJobs == null)
			{
				Main.LogError("Couldn't access private field \"allJobs\" of JobsManager singleton");
				allJobs = new List<Job>();
			}
			var jobToJobCars = (Dictionary<Job, HashSet<TrainCar>>) AccessTools.Field(typeof(JobsManager), "jobToJobCars").GetValue(jobsManager);
			if (jobToJobCars == null)
			{
				Main.LogError("Couldn't access private field \"jobToJobCars\" of JobsManager singleton");
				jobToJobCars = new Dictionary<Job, HashSet<TrainCar>>();
			}
			foreach (var job in allJobs)
			{
				yield return null;

				if (!jobToJobCars.TryGetValue(job, out HashSet<TrainCar> jobCars)) { continue; }

				carsWithJobs.UnionWith(jobCars.Select(trainCar => trainCar.logicCar));
			}

			Main.LogDebug(() => $"Found {carsWithJobs.Count()} cars with jobs assigned.");

			// Filter out cars with assigned jobs
			yield return null;
			carsInYard.ExceptWith(carsWithJobs);

			Main.LogDebug(() => $"Found {carsInYard.Count()} jobless cars in {yardId} yard and player's train.");

			int stationMinWagonsPerJob = proceduralRuleset.minCarsPerJob;
			int maxWagonsPerJob = Math.Min(proceduralRuleset.maxCarsPerJob, LicenseManager.Instance.GetMaxNumberOfCarsPerJobWithAcquiredJobLicenses());
			int maxShuntingStorageTracks = proceduralRuleset.maxShuntingStorageTracks;
			bool haulStartingJobSupported = proceduralRuleset.haulStartingJobSupported;
			bool unloadStartingJobSupported = proceduralRuleset.unloadStartingJobSupported;
			bool loadStartingJobSupported = proceduralRuleset.loadStartingJobSupported;

			yield return null;
			List<CargoGroup> licensedInboundCargoGroups = (
				from cargoGroup in proceduralRuleset.inputCargoGroups
				where LicenseManager_Patches.IsLicensedForCargoTypes(cargoGroup.cargoTypes)
				select cargoGroup
			).ToList();
			Main.LogDebug(() => $"Licensed inbound cargo groups: [{string.Join(", ", licensedInboundCargoGroups.Select(cg => string.Join(", ", cg.cargoTypes)))}]");

			yield return null;
			List<CargoGroup> licensedOutboundCargoGroups = (
				from cargoGroup in proceduralRuleset.outputCargoGroups
				where LicenseManager_Patches.IsLicensedForCargoTypes(cargoGroup.cargoTypes)
				select cargoGroup
			).ToList();
			Main.LogDebug(() => $"Licensed outbound cargo groups: [{string.Join(", ", licensedOutboundCargoGroups.Select(cg => string.Join(", ", cg.cargoTypes)))}]");

			// TrainCar is necessary for some operations, so convert once upfront
			HashSet<TrainCar> wagonsInYard = Utilities.ConvertLogicCarsToTrainCars(carsInYard).ToHashSet();

			// Group cars based on what type of job to generate for them
			var wagonsForLoading = new HashSet<TrainCar>();
			var wagonsForHauling = new HashSet<TrainCar>();
			var wagonsForUnloading = new HashSet<TrainCar>();
			var wagonsForLogistics = new HashSet<TrainCar>();

			if (loadStartingJobSupported)
			{
				// Find all cars available for loading jobs
				foreach (TrainCar wagon in wagonsInYard)
				{
					yield return null;

					// Must be empty to be loaded
					if (wagon.logicCar.CurrentCargoTypeInCar != CargoType.None) { continue; }
					// Must be able to hold at least one licensed outbound cargo
					if (!licensedOutboundCargoGroups.Any(
						cargoGroup => cargoGroup.cargoTypes.Any(
							cargoType => TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(wagon.logicCar.carType.parentType)
						)
					)) { continue; }

					wagonsForLoading.Add(wagon);
				}

				wagonsInYard.ExceptWith(wagonsForLoading);
				Main.LogDebug(() => $"Found {wagonsForLoading.Count} cars for shunting load jobs.");
			}
			else { Main.LogDebug(() => $"{yardId} doesn't support shunting load jobs."); }

			if (haulStartingJobSupported)
			{
				// Find all cars available for transport jobs
				foreach (TrainCar wagon in wagonsInYard)
				{
					yield return null;

					// Must not be empty
					if (wagon.logicCar.CurrentCargoTypeInCar == CargoType.None) { continue; }
					// Must be holding licensed outbound cargo
					if (!licensedOutboundCargoGroups.Any(
						cargoGroup => cargoGroup.cargoTypes.Any(
							cargoType => cargoType == wagon.logicCar.CurrentCargoTypeInCar
						)
					)) { continue; }
					// If an equipment reservation exists, it must originate here
					if (ReservationManager.Instance.TryGetReservation(wagon, out Reservation? reservation) && reservation.OutboundYardID != yardId) { continue; }

					wagonsForHauling.Add(wagon);
				}

				wagonsInYard.ExceptWith(wagonsForHauling);
				Main.LogDebug(() => $"Found {wagonsForHauling.Count} cars for transport jobs.");
			}
			else { Main.LogDebug(() => $"{yardId} doesn't support transport jobs."); }

			if (unloadStartingJobSupported)
			{
				// Find all cars available for unloading jobs
				foreach (TrainCar wagon in wagonsInYard)
				{
					yield return null;

					// Must not be empty
					if (wagon.logicCar.CurrentCargoTypeInCar == CargoType.None) { continue; }
					// Must be holding licensed inbound cargo
					if (!licensedInboundCargoGroups.Any(
						cargoGroup => cargoGroup.cargoTypes.Any(
							cargoType => cargoType == wagon.logicCar.CurrentCargoTypeInCar
						)
					)) { continue; }
					// If an equipment reservation exists, it must be destined for here
					if (ReservationManager.Instance.TryGetReservation(wagon, out Reservation? reservation) && reservation.InboundYardID != yardId) { continue; }

					wagonsForUnloading.Add(wagon);
				}

				wagonsInYard.ExceptWith(wagonsForUnloading);
				Main.LogDebug(() => $"Found {wagonsForUnloading.Count} cars for shunting unload jobs.");
			}
			else { Main.LogDebug(() => $"{yardId} doesn't support shunting unload jobs."); }

			if (wagonsInYard.Count > 0)
			{
				// Find all cars available for logistic jobs
				foreach (TrainCar wagon in wagonsInYard)
				{
					yield return null;

					// Must be empty
					if (wagon.logicCar.CurrentCargoTypeInCar != CargoType.None) { continue; }
					Main.LogDebug(() => $"{wagon.ID} ({wagon.carLivery.id}) {(Utilities.CanWagonHoldCargo(wagon) ? "can" : "cannot")} load cargo");
					// Must be able to hold cargo
					if (!Utilities.CanWagonHoldCargo(wagon)) { continue; }

					wagonsForLogistics.Add(wagon);
				}

				wagonsInYard.ExceptWith(wagonsForLogistics);
				Main.LogDebug(() => $"Found {wagonsForLogistics.Count} cars for logistic jobs.");
			}

			Main.LogDebug(() => $"Excluding {wagonsInYard.Count} cars as incompatible.");

			/**
			 * SHUNTING LOAD
			 */
			int attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			var loadingJobs = new List<JobChainController>();
			while (wagonsForLoading.Count > 0 && attemptsRemaining-- > 0)
			{
				StationController origin = stationController;

				// These are expensive operations, so we'll yield for a frame around them
				yield return null;
				Dictionary<(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID), HashSet<TrainCar>> associations =
					CreateWagonAssociations(licensedOutboundCargoGroups, wagonsForLoading);
				yield return null;
				(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID, HashSet<TrainCar> Wagons) association =
					ChooseAssociation(associations);

				yield return null;
				// This ensures a job will be generated even if there aren't technically enough total wagons
				int targetMinWagonsPerJob = association.Wagons.Count < 2 * stationMinWagonsPerJob ? association.Wagons.Count : stationMinWagonsPerJob;
				int absoluteMinWagonsPerJob = Math.Min(stationMinWagonsPerJob, association.Wagons.Count);

				yield return null;
				IEnumerable<Track> warehouseTracks = origin.logicStation.yard
					.GetWarehouseMachinesThatSupportCargoTypes(association.CargoGroup.cargoTypes)
					.Select(warehouseManchine => warehouseManchine.WarehouseTrack);
				double maxWarehouseTrackLength = GetLongestTrackLength(warehouseTracks);

				yield return null;
				IEnumerable<Track> outboundTracks = origin.logicStation.yard.TransferOutTracks;
				double maxOutboundTrackLength = GetLongestTrackLength(outboundTracks, freeSpaceOnly: true);

				// The limit is the shorter of the longest warehouse track and the longest outbound track
				double maxTrackLength = Math.Min(maxWarehouseTrackLength, maxOutboundTrackLength);
				Main.LogDebug(() => $"maximum warehouse track length: {maxWarehouseTrackLength}m\n\tmaximum outbound track length: {maxOutboundTrackLength}m\n\tmaximum track length to use for splitting wagon groups: {maxTrackLength}m");

				yield return null;
				IEnumerable<CoupledSetData> coupledSets =
					GroupWagonsByCoupled(rng, association.Wagons, targetMinWagonsPerJob, maxWagonsPerJob, maxTrackLength)
						.RandomSorting(rng);

				yield return null;
				int countSets = coupledSets.Count();
				int maxSets = Math.Min(countSets, maxShuntingStorageTracks);
				int targetSets = rng.Next(maxSets) + 1;

				// Use the randomly chosen sets as a starting point, but attempt to satisfy min/max counts/lengths
				IEnumerable<CoupledSetData> coupledSetsForGeneration = coupledSets.Take(targetSets);
				int expansion = 0;
				int reduction = 0;
				bool isTargetMinWagonsPerJobSatisfied = false;
				bool isAbsoluteMinWagonsPerJobSatisfied = false;
				bool isMaxWagonsPerJobSatisfied = false;
				bool isMaxTrainLengthSatisfied = false;
				while (expansion < countSets && reduction < countSets)
				{
					yield return null;
					IEnumerable<CoupledSetData> currentSliceOfCoupledSets = coupledSets.SkipTakeCyclical(reduction, targetSets + expansion - reduction);
					int wagonCount = currentSliceOfCoupledSets.Aggregate(0, (count, coupledSet) => count + coupledSet.Wagons.Count());
					double trainLength = currentSliceOfCoupledSets.Aggregate(0d, (length, coupledSet) => length + coupledSet.Length);
					bool currentSliceSatisfiesAbsoluteMinWagonsPerJob = wagonCount >= absoluteMinWagonsPerJob;
					bool currentSliceSatisfiesMaxWagonsPerJob = wagonCount <= maxWagonsPerJob;
					bool currentSliceSatisfiesMaxTrainLength = trainLength <= maxTrackLength;

					int nextExpansion = expansion + 1;
					if (!currentSliceSatisfiesAbsoluteMinWagonsPerJob && targetSets + nextExpansion - reduction <= maxSets)
					{
						expansion = nextExpansion;
						continue;
					}

					int nextReduction = reduction + 1;
					if ((!currentSliceSatisfiesMaxWagonsPerJob || !currentSliceSatisfiesMaxTrainLength) && targetSets + expansion - nextReduction > 0)
					{
						reduction = nextReduction;
						continue;
					}

					// At this point, all required constraints are met. Only optional constrains remain.
					coupledSetsForGeneration = currentSliceOfCoupledSets;
					isAbsoluteMinWagonsPerJobSatisfied = currentSliceSatisfiesAbsoluteMinWagonsPerJob;
					isMaxWagonsPerJobSatisfied = currentSliceSatisfiesMaxWagonsPerJob;
					isMaxTrainLengthSatisfied = currentSliceSatisfiesMaxTrainLength;

					if (wagonCount < targetMinWagonsPerJob && targetSets + nextExpansion - reduction <= maxSets)
					{
						expansion = nextExpansion;
						continue;
					}
					isTargetMinWagonsPerJobSatisfied = true;

					// All required and optional constrains are met. No need to continue looping.
					break;
				}

				if (!isAbsoluteMinWagonsPerJobSatisfied || !isMaxWagonsPerJobSatisfied || !isMaxTrainLengthSatisfied)
				{
					Main.LogWarning(
						$"Couldn't find a grouping of coupled sets to satisfy required car count and/or train length constraints.\n" +
						$"required car count range: [{absoluteMinWagonsPerJob}, {maxWagonsPerJob}]\n" +
						$"maximum warehouse track length: {maxWarehouseTrackLength}\n" +
						$"maximum outbound track length: {maxOutboundTrackLength}\n" +
						$"coupled sets: {WagonGroupsToString(coupledSets.Select(data => data.Wagons))}\n" +
						$"coupled set count: {countSets}\n" +
						$"maximum set count: {maxSets}\n" +
						$"target set count: {targetSets}\n" +
						$"expansion: {expansion}\n" +
						$"reduction: {reduction}\n"
					);
					continue;
				}

				Main.LogDebug(() =>
					$"Attempting shunting load job generation after satisfying {(isTargetMinWagonsPerJobSatisfied ? "all" : "required")} car count and train length constraints.\n" +
					$"coupled sets for generation: {WagonGroupsToString(coupledSetsForGeneration.Select(data => data.Wagons))}"
				);

				yield return null;
				List<List<Car>> carSetsForJob = (
					from data in coupledSetsForGeneration
					select (
						from wagon in data.Wagons
						select wagon.logicCar
					).ToList()
				).ToList();

				yield return null;
				JobChainController? jobChainController = ProceduralJobGenerators.GenerateLoadChainJobForCars(rng, carSetsForJob, association.CargoGroup, origin);

				if (jobChainController != null)
				{
					yield return null;
					loadingJobs.Add(jobChainController);
					List<TrainCar> wagonsForJob = jobChainController.trainCarsForJobChain;
					wagonsForLoading.ExceptWith(wagonsForJob);
					Main.LogDebug(() => $"Generated shunting load job with cars {string.Join(", ", wagonsForJob.Select(wagon => wagon.ID))}.");
				}
			}
			int attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - Math.Max(0, attemptsRemaining) - loadingJobs.Count;
			Main.Log($"Generated {loadingJobs.Count} shunting load jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForLoading.Count} cars not receiving jobs.");

			/**
			 * TRANSPORT
			 */
			attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			var haulingJobs = new List<JobChainController>();
			while (wagonsForHauling.Count > 0 && attemptsRemaining-- > 0)
			{
				// These are expensive operations, so we'll yield for a frame around them
				yield return null;
				Dictionary<(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID), HashSet<TrainCar>> associations =
					CreateWagonAssociations(licensedOutboundCargoGroups, wagonsForHauling);
				yield return null;
				(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID, HashSet<TrainCar> Wagons) association =
					ChooseAssociation(associations);

				yield return null;
				// This ensures a job will be generated even if there aren't technically enough total wagons
				int targetMinWagonsPerJob = association.Wagons.Count < 2 * stationMinWagonsPerJob ? association.Wagons.Count : stationMinWagonsPerJob;
				int absoluteMinWagonsPerJob = Math.Min(stationMinWagonsPerJob, association.Wagons.Count);

				yield return null;
				// Selects a station:
				//   - the reservations' inbound station, if reservations exist for these wagons
				//   - a random inbound station, if no reservations exist for these wagons
				StationController destination = association.CargoGroup.stations
					.Where(station => association.InboundYardID == null || station.stationInfo.YardID == association.InboundYardID)
					.ElementAtRandom(rng);
				StationController origin = stationController;

				yield return null;
				List<Track> inboundTracks = destination.logicStation.yard.TransferInTracks;
				double maxInboundTrackLength = GetLongestTrackLength(inboundTracks, freeSpaceOnly: true);
				Main.LogDebug(() => $"maximum inbound track length: {maxInboundTrackLength}m");

				yield return null;
				IEnumerable<CoupledSetData> coupledSets =
					GroupWagonsByCoupled(rng, association.Wagons, targetMinWagonsPerJob, maxWagonsPerJob, maxInboundTrackLength)
						.RandomSorting(rng);

				yield return null;
				CoupledSetData? coupledSet = null;
				CoupledSetData? possibleCoupledSet = null;
				foreach (CoupledSetData data in coupledSets)
				{
					if (data.Wagons.Count >= targetMinWagonsPerJob)
					{
						coupledSet = data;
						break;
					}
					if (possibleCoupledSet == null && data.Wagons.Count >= absoluteMinWagonsPerJob)
					{
						possibleCoupledSet = data;
					}
				}

				yield return null;
				CoupledSetData? coupledSetForGeneration = coupledSet ?? possibleCoupledSet;
				if (coupledSetForGeneration == null)
				{
					Main.LogWarning(
						$"Couldn't find a coupled set to satisfy required car count and/or train length constraints.\n" +
						$"required car count range: [{absoluteMinWagonsPerJob}, {maxWagonsPerJob}]\n" +
						$"maximum inbound track length: {maxInboundTrackLength}\n" +
						$"coupled sets: {WagonGroupsToString(coupledSets.Select(data => data.Wagons))}\n"
					);
					continue;
				}

				Main.LogDebug(() =>
					$"Attempting transport job generation after satisfying {(coupledSet != null ? "all" : "required")} car count and train length constraints.\n" +
					$"coupled set for generation: {WagonGroupsToString(new List<CoupledSetData>() { coupledSetForGeneration }.Select(data => data.Wagons))}"
				);

				yield return null;
				List<Car> carsForJob = coupledSetForGeneration.Wagons.Select(wagon => wagon.logicCar).ToList();

				yield return null;
				JobChainController? jobChainController = ProceduralJobGenerators.GenerateHaulChainJobForCars(rng, carsForJob, origin, destination);

				if (jobChainController != null)
				{
					yield return null;
					haulingJobs.Add(jobChainController);
					List<TrainCar> wagonsForJob = jobChainController.trainCarsForJobChain;
					wagonsForHauling.ExceptWith(wagonsForJob);
					Main.LogDebug(() => $"Generated transport job with cars {string.Join(", ", wagonsForJob.Select(tc => tc.ID))}.");
				}
			}
			attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - Math.Max(0, attemptsRemaining) - haulingJobs.Count;
			Main.Log($"Generated {haulingJobs.Count} transport jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForHauling.Count} cars not receiving jobs.");

			/**
			 * SHUNTING UNLOAD
			 */
			attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			var unloadingJobs = new List<JobChainController>();
			while (wagonsForUnloading.Count > 0 && attemptsRemaining-- > 0)
			{
				// These are expensive operations, so we'll yield for a frame around them
				yield return null;
				Dictionary<(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID), HashSet<TrainCar>> associations =
					CreateWagonAssociations(licensedInboundCargoGroups, wagonsForUnloading);
				yield return null;
				(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID, HashSet<TrainCar> Wagons) association =
					ChooseAssociation(associations);

				yield return null;
				// This ensures a job will be generated even if there aren't technically enough total wagons
				int targetMinWagonsPerJob = association.Wagons.Count < 2 * stationMinWagonsPerJob ? association.Wagons.Count : stationMinWagonsPerJob;
				int absoluteMinWagonsPerJob = Math.Min(stationMinWagonsPerJob, association.Wagons.Count);

				yield return null;
				// Selects a station:
				//   - the reservations' outbound station, if reservations exist for these wagons
				//   - a random inbound station, if no reservations exist for these wagons
				StationController origin = association.CargoGroup.stations
					.Where(station => association.OutboundYardID == null || station.stationInfo.YardID == association.OutboundYardID)
					.ElementAtRandom(rng);
				StationController destination = stationController;

				yield return null;
				int maxTracks = maxShuntingStorageTracks;
				for (; maxTracks > 1; --maxTracks)
				{
					if (association.Wagons.Count / maxTracks >= stationMinWagonsPerJob)
					{
						break;
					}
				}
				int trackCount = rng.Next(maxTracks) + 1;
				List<Track> storageTracks = destination.logicStation.yard.StorageTracks;
				IEnumerable<double> storageTrackUnoccupiedLengths = storageTracks
					.Select(track => track.length - track.OccupiedLength)
					.Where(unoccupiedLength => unoccupiedLength > 0d)
					.OrderByDescending(unoccupiedLength => unoccupiedLength);

				yield return null;
				// Multiplying by a fractional factor attempts to accommodate for track lengths that aren't perfect multiples of wagon lengths
				const double UNLOADING_STORAGE_TRACK_SPACE_FACTOR = 0.9d;
				double maxStorageTracksLength = UNLOADING_STORAGE_TRACK_SPACE_FACTOR * storageTrackUnoccupiedLengths
					.Take(trackCount) // Should this use a randomly selected target instead?
					.Aggregate(0d, (totalUnoccupiedLength, trackUnoccupiedLength) => totalUnoccupiedLength + trackUnoccupiedLength);

				yield return null;
				IEnumerable<Track> warehouseTracks = destination.logicStation.yard
					.GetWarehouseMachinesThatSupportCargoTypes(association.CargoGroup.cargoTypes)
					.Select(warehouseManchine => warehouseManchine.WarehouseTrack);
				double maxWarehouseTrackLength = GetLongestTrackLength(warehouseTracks);

				// The limit is the shorter of the longest warehouse track and the sum of the longest storage tracks
				double maxTrackLength = Math.Min(maxWarehouseTrackLength, maxStorageTracksLength);
				Main.LogDebug(() => $"maximum warehouse track length: {maxWarehouseTrackLength}m\n\tmaximum storage tracks length: {maxStorageTracksLength}m / {trackCount} tracks\n\tmaximum track length to use for splitting wagon groups: {maxTrackLength}m");

				yield return null;
				IEnumerable<CoupledSetData> coupledSets =
					GroupWagonsByCoupled(rng, association.Wagons, targetMinWagonsPerJob, maxWagonsPerJob, maxTrackLength)
						.RandomSorting(rng);

				yield return null;
				CoupledSetData? coupledSet = null;
				CoupledSetData? possibleCoupledSet = null;
				foreach (CoupledSetData data in coupledSets)
				{
					if (data.Wagons.Count >= targetMinWagonsPerJob)
					{
						coupledSet = data;
						break;
					}
					if (possibleCoupledSet == null && data.Wagons.Count >= absoluteMinWagonsPerJob)
					{
						possibleCoupledSet = data;
					}
				}

				yield return null;
				CoupledSetData? coupledSetForGeneration = coupledSet ?? possibleCoupledSet;
				if (coupledSetForGeneration == null)
				{
					Main.LogWarning(
						$"Couldn't find a coupled set to satisfy required car count and/or train length constraints.\n" +
						$"required car count range: [{absoluteMinWagonsPerJob}, {maxWagonsPerJob}]\n" +
						$"maximum storage track length: {maxTrackLength}\n" +
						$"coupled sets: {WagonGroupsToString(coupledSets.Select(data => data.Wagons))}\n"
					);
					continue;
				}

				Main.LogDebug(() =>
					$"Attempting transport job generation after satisfying {(coupledSet != null ? "all" : "required")} car count and train length constraints.\n" +
					$"coupled set for generation: {WagonGroupsToString(new List<CoupledSetData>() { coupledSetForGeneration }.Select(data => data.Wagons))}"
				);

				yield return null;
				List<Car> carsForJob = coupledSetForGeneration.Wagons.Select(wagon => wagon.logicCar).ToList();

				yield return null;
				JobChainController? jobChainController = ProceduralJobGenerators.GenerateUnloadChainJobForCars(rng, carsForJob, origin, destination);

				if (jobChainController != null)
				{
					yield return null;
					unloadingJobs.Add(jobChainController);
					List<TrainCar> wagonsForJob = jobChainController.trainCarsForJobChain;
					wagonsForUnloading.ExceptWith(wagonsForJob);
					Main.LogDebug(() => $"Generated transport job with cars {string.Join(", ", wagonsForJob.Select(tc => tc.ID))}.");
				}
			}
			attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - Math.Max(0, attemptsRemaining) - unloadingJobs.Count;
			Main.Log($"Generated {unloadingJobs.Count} shunting unload jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForUnloading.Count} cars not receiving jobs.");

			/**
			 * LOGISTIC
			 */
			attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			var logisticJobs = new List<JobChainController>();
			while (wagonsForLogistics.Count > 0 && attemptsRemaining-- > 0)
			{
				// These are expensive operations, so we'll yield for a frame around them
				yield return null;
				// Find stations that load cargo onto available wagons
				IEnumerable<StationController> availableStations = LogicController.Instance.YardIdToStationController.Values
					.Where(station => station.proceduralJobsRuleset.outputCargoGroups
						.Where(cargoGroup => LicenseManager_Patches.IsLicensedForCargoTypes(cargoGroup.cargoTypes))
						.Any(cargoGroup => cargoGroup.cargoTypes
							.Any(cargoType => cargoType.ToV2().loadableCarTypes.Select(info => info.carType)
								.Intersect(wagonsForLogistics.Select(wagon => wagon.carLivery.parentType)).Count() > 0)));
				yield return null;
				StationController destination = availableStations.ElementAtRandom(rng);
				StationController origin = stationController;
				yield return null;
				List<CargoGroup> licesenedCargoGroupsAtDestination = (
					from cargoGroup in destination.proceduralJobsRuleset.outputCargoGroups
					where LicenseManager_Patches.IsLicensedForCargoTypes(cargoGroup.cargoTypes)
					select cargoGroup
				).ToList();
				Dictionary<(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID), HashSet<TrainCar>> associations =
					CreateWagonAssociations(licesenedCargoGroupsAtDestination, wagonsForLogistics);
				yield return null;
				(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID, HashSet<TrainCar> Wagons) association =
					ChooseAssociation(associations);

				yield return null;
				// This ensures a job will be generated even if there aren't technically enough total wagons
				int targetMinWagonsPerJob = association.Wagons.Count < 2 * stationMinWagonsPerJob ? association.Wagons.Count : stationMinWagonsPerJob;
				int absoluteMinWagonsPerJob = Math.Min(stationMinWagonsPerJob, association.Wagons.Count);

				yield return null;
				List<Track> storageTracks = destination.logicStation.yard.StorageTracks;
				double maxStorageTrackLength = GetLongestTrackLength(storageTracks, freeSpaceOnly: true);
				Main.LogDebug(() => $"maximum storage track length: {maxStorageTrackLength}m");

				yield return null;
				IEnumerable<CoupledSetData> coupledSets =
					GroupWagonsByCoupled(rng, association.Wagons, targetMinWagonsPerJob, maxWagonsPerJob, maxStorageTrackLength)
						.RandomSorting(rng);

				yield return null;
				CoupledSetData? coupledSet = null;
				CoupledSetData? possibleCoupledSet = null;
				foreach (CoupledSetData data in coupledSets)
				{
					if (data.Wagons.Count >= targetMinWagonsPerJob)
					{
						coupledSet = data;
						break;
					}
					if (possibleCoupledSet == null && data.Wagons.Count >= absoluteMinWagonsPerJob)
					{
						possibleCoupledSet = data;
					}
				}

				yield return null;
				CoupledSetData? coupledSetForGeneration = coupledSet ?? possibleCoupledSet;
				if (coupledSetForGeneration == null)
				{
					Main.LogWarning(
						$"Couldn't find a coupled set to satisfy required car count and/or train length constraints.\n" +
						$"required car count range: [{absoluteMinWagonsPerJob}, {maxWagonsPerJob}]\n" +
						$"maximum storage track length: {maxStorageTrackLength}\n" +
						$"coupled sets: {WagonGroupsToString(coupledSets.Select(data => data.Wagons))}\n"
					);
					continue;
				}

				Main.LogDebug(() =>
					$"Attempting logistic job generation after satisfying {(coupledSet != null ? "all" : "required")} car count and train length constraints.\n" +
					$"coupled set for generation: {WagonGroupsToString(new List<CoupledSetData>() { coupledSetForGeneration }.Select(data => data.Wagons))}"
				);

				yield return null;
				List<Car> carsForJob = coupledSetForGeneration.Wagons.Select(wagon => wagon.logicCar).ToList();

				yield return null;
				JobChainController? jobChainController = ProceduralJobGenerators.GenerateLogisticChainJobForCars(rng, carsForJob, origin, destination);

				if (jobChainController != null)
				{
					yield return null;
					logisticJobs.Add(jobChainController);
					List<TrainCar> wagonsForJob = jobChainController.trainCarsForJobChain;
					wagonsForLogistics.ExceptWith(wagonsForJob);
					Main.LogDebug(() => $"Generated logistic job with cars {string.Join(", ", wagonsForJob.Select(tc => tc.ID))}.");
				}
			}
			attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - Math.Max(0, attemptsRemaining) - logisticJobs.Count;
			Main.Log($"Generated {logisticJobs.Count} logistic jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForLogistics.Count} cars not receiving jobs.");
		}

		if (onComplete != null)
		{
			onComplete();
		}
		yield break;
	}

	private static IEnumerable<Track> GetTracksByYardID (string yardId)
	{
		var allTracks = RailTrackRegistry.Instance.AllTracks;
		return from railTrack in allTracks where railTrack.logicTrack.ID.yardId == yardId select railTrack.logicTrack;
	}

	/**
	 * Associates cars with the cargo groups they can load.
	 * Cars will be unique for any single cargo group, but may be associated with multiple cargo groups.
	 */
	private static Dictionary<
		(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID),
		HashSet<TrainCar>
	> CreateWagonAssociations(List<CargoGroup> cargoGroups, HashSet<TrainCar> wagons)
	{
		var associations = new Dictionary<(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID), HashSet<TrainCar>>();

		foreach (CargoGroup cargoGroup in cargoGroups)
		{
			foreach (CargoType cargoType in cargoGroup.cargoTypes)
			{
				foreach (TrainCar wagon in wagons)
				{
					if (cargoType.ToV2().IsLoadableOnCarType(wagon.logicCar.carType.parentType))
					{
						(CargoGroup, string?, string?) key = (cargoGroup, null, null);
						if (ReservationManager.Instance.TryGetReservation(wagon, out Reservation? reservation))
						{
							key = (cargoGroup, reservation.OutboundYardID, reservation.InboundYardID);
						}
						if (!associations.TryGetValue(key, out HashSet<TrainCar> association))
						{
							associations[key] = association = new HashSet<TrainCar>();
						}
						association.Add(wagon);
					}
				}
			}
		}

		return associations;
	}

	private static (CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID, HashSet<TrainCar> Wagons) ChooseAssociation(
		Dictionary<(CargoGroup CargoGroup, string? OutboundYardID, string? InboundYardID), HashSet<TrainCar>> associations)
	{
		if (associations.Count < 1)
		{
			throw new ArgumentException($"Associations must contain one or more kvpairs, but it was empty!");
		}

		var chosenAssociation = associations.Skip(1).Aggregate(
			associations.ElementAt(0),
			(accumulator, kvpair) => {
				int accumulatorSize = accumulator.Value.Count;
				int kvpairSize = kvpair.Value.Count;
				return kvpairSize > accumulatorSize ? kvpair : accumulator;
			}
		);

		return (
			chosenAssociation.Key.CargoGroup,
			chosenAssociation.Key.OutboundYardID,
			chosenAssociation.Key.InboundYardID,
			chosenAssociation.Value
		);
	}

	private static List<CoupledSetData> GroupWagonsByCoupled(Random rng, HashSet<TrainCar> wagons, int minCount, int maxCount, double maxLength)
	{
		var _wagons = new HashSet<TrainCar>(wagons); // Cloning the hash set so the input isn't mutated
		var coupledSets = new List<CoupledSetData>();

		while (_wagons.Count > 0)
		{
			var coupled = new HashSet<TrainCar>();
			double coupledLength = 0;
			Queue<TrainCar> wagonsQ = GetNextWagonQueueFromPool(_wagons); // Finds all wagons from the pool that are coupled to the next wagon from the pool

			// The first wagon is guaranteed to either make it into a group or not fit on any track
			// This prevents infinite looping
			_wagons.Remove(wagonsQ.Peek());

			while (wagonsQ.Count > 0 && coupled.Count < maxCount)
			{
				TrainCar currentWagon = wagonsQ.Dequeue();
				double wagonLength = currentWagon.logicCar.length;
				if (coupled.Count > 0)
				{
					// GetSeparationLengthBetweenCars includes space at the end on each side.
					// Passing 0 gets the length of a single separation.
					wagonLength += CarSpawner.Instance.GetSeparationLengthBetweenCars(0);
				}
				else
				{
					// This accounts for the separation on both ends of the coupled set
					wagonLength += CarSpawner.Instance.GetSeparationLengthBetweenCars(1);
				}
				if (coupledLength + wagonLength > maxLength)
				{
					Main.LogDebug(() => $"Splitting coupled wagons due to length ({coupled.Count}/{maxCount})");
					break;
				} // No more space on the limiting track!
				coupledLength += wagonLength;

				coupled.Add(currentWagon);
				_wagons.Remove(currentWagon);

				// Random chance to split into a smaller group
				// Shouldn't split when below the minumum wagon count for grouped wagons or remaining queued wagons
				// Probability of splitting increases as the number grouped wagons approaches the maximum
				if (coupled.Count >= minCount && wagonsQ.Count >= minCount)
				{
					const int RANDOM_SPLIT_SCALING_FACTOR = 2;
					int result = rng.Next((maxCount - coupled.Count) * RANDOM_SPLIT_SCALING_FACTOR);
					if (result == 0)
					{
						Main.LogDebug(() => $"Splitting coupled wagons due to random chance ({coupled.Count}/{maxCount})");
						break;
					}
				}

				// We don't check if the wagons queue count is less than the minimum wagon count because
				// we don't want to split off a group smaller than the minimum wagon count
				// (unless we have to because of length requirements)
				if (coupled.Count >= minCount && wagonsQ.Count == minCount)
				{
					double fullLength = coupledLength + CarSpawner.Instance.GetTotalCarsLength(wagonsQ.Select(tc => tc.logicCar).ToList())
						+ CarSpawner.Instance.GetSeparationLengthBetweenCars(wagonsQ.Count - 1); // Adds 1 internally, but we've already accounted for the separation distance at both ends

					if (fullLength > maxLength)
					{
						Main.LogDebug(() => $"Splitting coupled wagons due to remaining queue count ({coupled.Count}/{wagonsQ.Count})");
						break;
					}
				}
			}

			if (coupled.Count > 0)
			{
				coupledSets.Add(new CoupledSetData(coupled, coupledLength));
				Main.LogDebug(() => $"Grouped coupled wagons ({coupled.Count}/{maxCount})\n\tcoupled length: {coupledLength}m\n\tmax length: {maxLength}m");
			}
			else
			{
				Main.LogError("Coupled set was empty. This should never happen!");
			}
		}

		Main.LogDebug(() => $"Grouped cars by coupled: {WagonGroupsToString(coupledSets.Select(coupledSet => coupledSet.Wagons))}");

		return coupledSets;
	}

	private static Queue<TrainCar> GetNextWagonQueueFromPool(IEnumerable<TrainCar> wagonPool)
	{
		IEnumerable<TrainCar> coupledWagons = new List<TrainCar>();
		TrainCar? firstWagon = wagonPool.FirstOrDefault();
		Main.LogDebug(() => $"Wagon pool has {wagonPool.Count()} wagons and first wagon is {(firstWagon == null ? "null" : "not null")}.");

		if (firstWagon != null)
		{
			coupledWagons = coupledWagons.Append(firstWagon);
			var seenWagons = new HashSet<TrainCar> { firstWagon };

			// Iterate "forward"
			TrainCar? currentWagon = wagonPool.FirstOrDefault(wagon => wagon == firstWagon.frontCoupler?.coupledTo?.train);
			while (currentWagon != null)
			{
				coupledWagons = coupledWagons.Prepend(currentWagon);
				seenWagons.Add(currentWagon);
				TrainCar? frontWagon = wagonPool.FirstOrDefault(wagon => wagon == currentWagon.frontCoupler?.coupledTo?.train);
				TrainCar? rearWagon = wagonPool.FirstOrDefault(wagon => wagon == currentWagon.rearCoupler?.coupledTo?.train);
				currentWagon = seenWagons.Contains(rearWagon) ? frontWagon : rearWagon;
			}

			// Iterate "backward"
			currentWagon = wagonPool.FirstOrDefault(wagon => wagon == firstWagon.rearCoupler?.coupledTo?.train);
			while (currentWagon != null)
			{
				coupledWagons = coupledWagons.Append(currentWagon);
				seenWagons.Add(currentWagon);
				TrainCar? frontWagon = wagonPool.FirstOrDefault(wagon => wagon == currentWagon.frontCoupler?.coupledTo?.train);
				TrainCar? rearWagon = wagonPool.FirstOrDefault(wagon => wagon == currentWagon.rearCoupler?.coupledTo?.train);
				currentWagon = seenWagons.Contains(rearWagon) ? frontWagon : rearWagon;
			}
		}

		var wagonsQ = new Queue<TrainCar>();
		coupledWagons.ToList().ForEach(wagonsQ.Enqueue);
		Main.LogDebug(() => $"Next wagon list ({coupledWagons.Count()})\nNext wagon queue ({wagonsQ.Count})");
		return wagonsQ;
	}

	private static string WagonGroupsToString(IEnumerable<IEnumerable<TrainCar>> wagonGroups)
	{
		return $"[{string.Join(", ", wagonGroups.Select(WagonGroupToString))}]";
	}

	private static string WagonGroupToString(IEnumerable<TrainCar> wagons)
	{
		return $"[{string.Join(", ", wagons.Select(wagon => wagon.ID))}]({wagons.Count()}|{CarSpawner.Instance.GetTotalCarsLength(wagons.Select(tc => tc.logicCar).ToList(), true)}m)";
	}

	private static double GetLongestTrackLength(IEnumerable<Track> tracks, bool freeSpaceOnly = false)
	{
		return tracks.Aggregate(0d, (maxLength, track) => {
			double trackLength = freeSpaceOnly ? YardTracksOrganizer.Instance.GetFreeSpaceOnTrack(track) : track.length;
			Main.LogDebug(() => $"track {track.ID} -> {trackLength}m{(freeSpaceOnly ? $" / {track.length}m" : "")}");
			return trackLength > maxLength ? trackLength : maxLength;
		});
	}

	private class CoupledSetData
	{
		public HashSet<TrainCar> Wagons { get; private set; }
		public double Length { get; private set; }

		public CoupledSetData(HashSet<TrainCar> wagons, double length)
		{
			Wagons = wagons;
			Length = length;
		}
	}
}
