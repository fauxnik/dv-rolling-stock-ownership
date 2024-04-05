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
using System.Text;

namespace RollingStockOwnership;

public class ProceduralJobsController
{
	private static readonly int MAX_JOB_GENERATION_ATTEMPTS = 30;
	private static Dictionary<StationController, ProceduralJobsController> instances = new Dictionary<StationController, ProceduralJobsController>();

	private StationController stationController;
	private List<Track> stationTracks;

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
	}

	public IEnumerator GenerateJobsCoro(Action onComplete, IEnumerable<Car>? carsToUse = null)
	{
		var log = new StringBuilder();
		int tickCount = Environment.TickCount;
		System.Random rng = new System.Random(tickCount);
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
				foreach (var track in stationTracks)
				{
					yield return null;

					// respawn equipment on track
					foreach (var equipment in manager.GetEquipmentOnTrack(track, false))
					{
						yield return null;
						equipment.Spawn();
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

			/**
			 * JOB GENERATION REWRITE NOTES
			 *
			 * 0. replace Equipment destination field with EquipmentReservation class
			 *     a. car ID
			 *     b. origin station ID
			 *     c. destination station ID
			 *     d. cargo type?
			 * 1. separate cars in yard into subsets based on loaded cargo and relevant EquipmentReservation
			 *     a. empty
			 *     b. loaded (inbound)
			 *     c. loaded (outbound)
			 * 2. for each subset of cars, create a mapping of all relevant cargo types supported by the station to cars that can contain that type
			 * 3. choose the cargo type with the most cars mapped to it and attempt to generate a job using those cars as the pool of available equipment
			 *     a. break ties with RNG?
			 * 4. upon successful generation, remove the involved cars from all mappings
			 * 5. repeat until all cars have been accounted for or no more jobs can be generated
			 */

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
				double maxOutboundTrackLength = GetLongestTrackLength(outboundTracks);

				// The limit is the shorter of the longest warehouse track and the longest outbound track
				double maxTrackLength = Math.Min(maxWarehouseTrackLength, maxOutboundTrackLength);

				yield return null;
				IEnumerable<CoupledSetData> coupledSets =
					GroupWagonsByCoupled(association.Wagons, maxWagonsPerJob, maxTrackLength)
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
			int attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - attemptsRemaining - loadingJobs.Count;
			log.AppendLine($"Generated {loadingJobs.Count} shunting load jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForLoading.Count} cars not receiving jobs.");

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
				double maxInboundTrackLength = GetLongestTrackLength(inboundTracks);

				yield return null;
				IEnumerable<CoupledSetData> coupledSets =
					GroupWagonsByCoupled(association.Wagons, maxWagonsPerJob, maxInboundTrackLength)
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
			attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - attemptsRemaining - haulingJobs.Count;
			log.AppendLine($"Generated {haulingJobs.Count} transport jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForHauling.Count} cars not receiving jobs.");

			/**
			 * SHUNTING UNLOAD
			 */
			attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			var unloadingJobs = new List<JobChainController>();
			while (wagonsForUnloading.Count > 0 && attemptsRemaining-- > 0)
			{}
			attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - attemptsRemaining - unloadingJobs.Count;
			log.AppendLine($"Generated {unloadingJobs.Count} shunting unload jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts and {wagonsForUnloading.Count} cars not receiving jobs.");

			// // loop, generating jobs for train cars, until all train cars are accounted for or we reach an upper bound of attempts
			// var carsQ = new Queue<Car>();
			// foreach (var car in carsInYard) { carsQ.Enqueue(car); }
			// var attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			// var jobsGenerated = 0;
			// while (attemptsRemaining > 0 && carsInYard.Count > 0 && carsQ.Count > 0)
			// {
			// 	yield return null;

			// 	var thisCar = carsQ.Dequeue();
			// 	if (!carsInYard.Contains(thisCar)) { continue; }

			// 	Main.LogDebug(() => $"Attempting to generate job for car {thisCar.ID}.");

			// 	JobChainController? jobChainController = null;
			// 	if (!(manager.FindByCarGUID(thisCar.carGuid) is Equipment thisEquipment)) { continue; }
			// 	var carsForJob = new HashSet<Car> { thisCar };

			// 	string jobType = "unknown";
			// 	var carType = thisCar.carType;
			// 	var cargoTypeInCar = thisCar.CurrentCargoTypeInCar;
			// 	if (cargoTypeInCar != CargoType.None)
			// 	{
			// 		if (haulStartingJobSupported && licensedOutputCargoGroups.Any(group => group.cargoTypes.Contains(cargoTypeInCar)))
			// 		{
			// 			// Player previously loaded car here, generate freight haul job
			// 			jobType = JobType.Transport.ToString();
			// 			var potentialCargoGroups = licensedOutputCargoGroups.Where(group => group.cargoTypes.Contains(cargoTypeInCar));
			// 			var countCargoGroups = potentialCargoGroups.Count();
			// 			var indexInCargoGroups = rng.Next(countCargoGroups);
			// 			var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

			// 			Main.LogDebug(() => $"Attempting to generate freight haul job using cargo group {cargoGroup}, {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

			// 			yield return null;
			// 			carsForJob.UnionWith(GetMatchingCoupledCars(thisEquipment, cargoGroup, carsInYard, maxCarsPerJob - carsForJob.Count));

			// 			if (carsForJob.Count > maxCarsPerJob)
			// 			{
			// 				int count = maxCarsPerJob - Math.Max(0, minCarsPerJob - (carsForJob.Count - maxCarsPerJob));
			// 				Main.LogDebug(() => $"Exceeded the maximum number of cars per job ({maxCarsPerJob}). Truncating set to {count} cars.");
			// 				carsForJob = carsForJob.ToList().Take(count).ToHashSet();
			// 			}

			// 			// Generate the job, but only if it meets the length requirements
			// 			if (carsForJob.Count >= minCarsPerJob && carsForJob.Count <= maxCarsPerJob)
			// 			{
			// 				Main.LogDebug(() => $"Generating freight haul job for {carsForJob.Count} cars: [{string.Join(", ", carsForJob.Select(car => car.ID))}]");
			// 				yield return null;
			// 				jobChainController = ProceduralJobGenerators.GenerateHaulChainJobForCars(rng, carsForJob.ToList(), cargoGroup, stationController);
			// 				Main.LogDebug(() => "Generation OK");
			// 			}
			// 			else
			// 			{
			// 				Main.LogDebug(() => $"Didn't meet the minimum number of cars per job ({minCarsPerJob}).");
			// 			}
			// 		}
			// 		else if (unloadStartingJobSupported && thisEquipment?.DestinationID == stationId && inputCargoGroups.Any(group => group.cargoTypes.Contains(cargoTypeInCar)))
			// 		{
			// 			// Player previously hauled car here, generate shunting unload job
			// 			jobType = JobType.ShuntingUnload.ToString();
			// 			var potentialCargoGroups = inputCargoGroups.Where(group => group.cargoTypes.Contains(cargoTypeInCar));
			// 			var countCargoGroups = potentialCargoGroups.Count();
			// 			var indexInCargoGroups = rng.Next(countCargoGroups);
			// 			var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

			// 			Main.LogDebug(() => $"Attempting to generate shunting unload job using cargo group {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

			// 			yield return null;
			// 			carsForJob.UnionWith(GetMatchingCoupledCars(thisEquipment, cargoGroup, carsInYard, maxCarsPerJob - carsForJob.Count));

			// 			if (carsForJob.Count > maxCarsPerJob)
			// 			{
			// 				int count = maxCarsPerJob - Math.Max(0, minCarsPerJob - (carsForJob.Count - maxCarsPerJob));
			// 				Main.LogDebug(() => $"Exceeded the maximum number of cars per job ({maxCarsPerJob}). Truncating set to {count} cars.");
			// 				carsForJob = carsForJob.ToList().Take(count).ToHashSet();
			// 			}

			// 			// Generate the job, but only if it meets the length requirements
			// 			if (carsForJob.Count >= minCarsPerJob && carsForJob.Count <= maxCarsPerJob)
			// 			{
			// 				Main.LogDebug(() => $"Generating shunting unload job for {carsForJob.Count} cars: [{string.Join(", ", carsForJob.Select(car => car.ID))}]");
			// 				yield return null;
			// 				jobChainController = ProceduralJobGenerators.GenerateUnloadChainJobForCars(rng, carsForJob.ToList(), cargoGroup, stationController);
			// 				Main.LogDebug(() => "Generation OK");
			// 			}
			// 			else
			// 			{
			// 				Main.LogDebug(() => $"Didn't meet the minimum number of cars per job ({minCarsPerJob}).");
			// 			}
			// 		}
			// 	}
			// 	else
			// 	{
			// 		if (loadStartingJobSupported && licensedOutputCargoGroups.Any(group => group.cargoTypes.Any(cargoType => TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(carType.parentType))))
			// 		{
			// 			// Station can load cargo into this car & player is licensed to do so, generate shunting load job
			// 			jobType = JobType.ShuntingLoad.ToString();
			// 			var potentialCargoGroups = licensedOutputCargoGroups.Where(group => group.cargoTypes.Any(cargoType => TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(carType.parentType)));
			// 			var countCargoGroups = potentialCargoGroups.Count();
			// 			var indexInCargoGroups = rng.Next(countCargoGroups);
			// 			var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

			// 			Main.LogDebug(() => $"Attempting to generate shunting load job using cargo group {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

			// 			yield return null;

			// 			// Find all equipment that matches the selected cargo group
			// 			var potentialEmptyCars = carsInYard.Where(car => car.CurrentCargoTypeInCar == CargoType.None && cargoGroup.cargoTypes.Any(cargoType => TransitionHelpers.ToV2(cargoType).IsLoadableOnCarType(carType.parentType))).ToList();
			// 			var potentialEquipment = potentialEmptyCars.Select(car => manager.FindByCarGUID(car.carGuid)).ToList();

			// 			yield return null;

			// 			// Group equipment into train sets
			// 			var contiguousEquipment = new List<HashSet<Equipment>>();
			// 			foreach (var currentEquipment in potentialEquipment)
			// 			{
			// 				if (currentEquipment == null) { continue; }

			// 				var currentCarGUID = currentEquipment.CarGUID;
			// 				var contiguousSet = contiguousEquipment.Find(set => set.Any(equipmentFromSet => equipmentFromSet.IsCoupledTo(currentCarGUID)));

			// 				if (contiguousSet == null)
			// 				{
			// 					contiguousSet = new HashSet<Equipment>();
			// 					contiguousEquipment.Add(contiguousSet);
			// 				}

			// 				contiguousSet.Add(currentEquipment);
			// 			}

			// 			LogContiguousEquipment(contiguousEquipment);

			// 			yield return null;

			// 			// Get the train set that includes the original car (and remove it from the list to avoid double processing)
			// 			var thisEquipmentSet = thisEquipment != null ? contiguousEquipment.Find(set => set.Contains(thisEquipment)) : new HashSet<Equipment>();
			// 			contiguousEquipment.Remove(thisEquipmentSet);

			// 			yield return null;

			// 			// Select train sets based on maximum requirements
			// 			var equipmentSetsForJob = new List<HashSet<Equipment>> { thisEquipmentSet };
			// 			for (var index = 0; equipmentSetsForJob.Count < maxShuntingStorageTracks && index < contiguousEquipment.Count; ++index)
			// 			{
			// 				var trainLengthSoFar = equipmentSetsForJob.Aggregate(0, (sum, set) => sum + set.Count);
			// 				var equipmentSet = contiguousEquipment[index];
			// 				if (trainLengthSoFar + equipmentSet.Count > maxCarsPerJob) { continue; }

			// 				equipmentSetsForJob.Add(equipmentSet);
			// 			}

			// 			yield return null;

			// 			// Add cars to carsForJob so that they'll be removed from carsInYard if a job is successfully generated
			// 			foreach (var equipmentSet in equipmentSetsForJob)
			// 			{
			// 				foreach (var equipment in equipmentSet)
			// 				{
			// 					if (equipment.GetLogicCar() is Car car) { carsForJob.Add(car); }
			// 					else { Main.LogError($"Logic car for equipment with ID {equipment.ID} not found. This shouldn't happen."); }
			// 				}
			// 			}

			// 			if (carsForJob.Count > maxCarsPerJob)
			// 			{
			// 				int count = maxCarsPerJob - Math.Max(0, minCarsPerJob - (carsForJob.Count - maxCarsPerJob));
			// 				Main.LogDebug(() => $"Exceeded the maximum number of cars per job ({maxCarsPerJob}). Truncating set to {count} cars.");
			// 				carsForJob = carsForJob.ToList().Take(count).ToHashSet();
			// 			}

			// 			// Generate the job, but only if it meets the length requirements
			// 			if (carsForJob.Count >= minCarsPerJob && carsForJob.Count <= maxCarsPerJob)
			// 			{
			// 				Main.LogDebug(() => $"Generating shunting load job for {carsForJob.Count} cars: [{string.Join(", ", carsForJob.Select(car => car.ID))}]");
			// 				yield return null;
			// 				var carSetsForJob =
			// 					from equipmentSet in equipmentSetsForJob
			// 					select (
			// 						from equipment in equipmentSet
			// 						where equipment.GetLogicCar() != null && carsForJob.Contains(equipment.GetLogicCar()!) // Cars may have been dropped to meet length requirements
			// 						select equipment.GetLogicCar()
			// 					).ToList();
			// 				jobChainController = ProceduralJobGenerators.GenerateLoadChainJobForCars(rng, carSetsForJob.ToList(), cargoGroup, stationController);
			// 				Main.LogDebug(() => "Generation OK");
			// 			}
			// 			else
			// 			{
			// 				Main.LogDebug(() => $"Didn't meet the minimum number of cars per job ({minCarsPerJob}).");
			// 			}
			// 		}
			// 	}

			// 	if (jobChainController != null)
			// 	{
			// 		// TODO: what do we do with it?
			// 		jobsGenerated++;
			// 		var trainCarsForJob = jobChainController.trainCarsForJobChain;
			// 		carsInYard.ExceptWith(from trainCar in trainCarsForJob select trainCar.logicCar);
			// 		log.Append($"Generated {jobType} job with cars {string.Join(", ", trainCarsForJob.Select(tc => tc.ID))}.\n");
			// 	}
			// 	else
			// 	{
			// 		// Try again, but only after attempting to generate jobs for other cars first
			// 		carsQ.Enqueue(thisCar);
			// 		--attemptsRemaining;
			// 	}
			// }

			// yield return null;

			// var attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - attemptsRemaining;
			// log.Append($"Generated a total of {jobsGenerated} jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts.");
		}

		Main.Log(log.ToString());
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

	private static HashSet<Car> GetMatchingCoupledCars(Equipment equipment, CargoGroup cargoGroup, HashSet<Car> carsInYard, int maxCarsToReturn)
	{
		var manager = RollingStockManager.Instance;
		var cars = new HashSet<Car>();
		if (manager == null) { return cars; }

		// Move outward from car, seeking adjacent coupled cars that match the cargo group
		var seekQ = new Queue<Equipment>();
		var seenEquipment = new HashSet<Equipment>();
		Equipment? coupledEquipment;
		coupledEquipment = manager.FindByCarGUID(equipment.CarGuidCoupledFront);
		if (coupledEquipment != null) { seekQ.Enqueue(coupledEquipment); }
		coupledEquipment = manager.FindByCarGUID(equipment.CarGuidCoupledRear);
		if (coupledEquipment != null) { seekQ.Enqueue(coupledEquipment); }
		while (seekQ.Count > 0 && cars.Count < maxCarsToReturn)
		{
			var possibleMatch = seekQ.Dequeue();
			seenEquipment.Add(possibleMatch);

			var possibleMatchLogicCar = possibleMatch.GetLogicCar();
			if (possibleMatchLogicCar == null || !carsInYard.Contains(possibleMatchLogicCar) || !cargoGroup.cargoTypes.Contains(possibleMatchLogicCar.CurrentCargoTypeInCar))
			{
				continue;
			}

			cars.Add(possibleMatchLogicCar);
			if (possibleMatch.IsCoupledFront)
			{
				coupledEquipment = manager.FindByCarGUID(possibleMatch.CarGuidCoupledFront);
				if (coupledEquipment != null && !seenEquipment.Contains(coupledEquipment)) { seekQ.Enqueue(coupledEquipment); }
			}
			if (possibleMatch.IsCoupledRear)
			{
				coupledEquipment = manager.FindByCarGUID(possibleMatch.CarGuidCoupledRear);
				if (coupledEquipment != null && !seenEquipment.Contains(coupledEquipment)) { seekQ.Enqueue(coupledEquipment); }
			}
		}

		return cars;
	}

	private static void LogContiguousEquipment(List<HashSet<Equipment>> contiguousEquipment)
	{
		static string joinEquipmentIDsFromHashSet(HashSet<Equipment> set) => string.Join(", ", set.Select(eq => eq.ID));
		var contiguousEquipmentIDs = string.Join(", ", contiguousEquipment.Select(set => $"[{joinEquipmentIDsFromHashSet(set)}]"));
		Main.LogDebug(() => $"Contiguous Equipment: [{contiguousEquipmentIDs}]");
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

	private static List<CoupledSetData> GroupWagonsByCoupled(HashSet<TrainCar> wagons, int maxCount, double maxLength)
	{
		var _wagons = new HashSet<TrainCar>(wagons); // Cloning the hash set so the input isn't mutated
		var coupledSets = new List<CoupledSetData>();

		while (_wagons.Count > 0)
		{
			var coupled = new HashSet<TrainCar>();
			double coupledLength = 0;
			var wagonsQ = new Queue<TrainCar>();
			wagonsQ.Enqueue(GetNextEdgeWagonFromPool(_wagons)!); // _wagons.Count is checked above, so this won't be null

			while (wagonsQ.Count > 0 && coupled.Count < maxCount)
			{
				TrainCar currentWagon = wagonsQ.Dequeue();
				double wagonLength = 0;
				if (coupledLength > 0)
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
				wagonLength += currentWagon.logicCar.length;
				if (coupledLength + wagonLength > maxLength) { break; } // No more space on the longest possible supported warehouse track!
				coupledLength += wagonLength;

				coupled.Add(currentWagon); // If the wagon made it into the queue, it was coupled to a wagon already added to the hash set (or it's the first wagon)
				_wagons.Remove(currentWagon); // The code that finds the coupled wagons relies on immediately removing the current wagon here to avoid a cycle/infinite loop

				TrainCar? coupledFront = _wagons.FirstOrDefault(wagon => wagon == currentWagon.frontCoupler?.coupledTo?.train);
				if (coupledFront != null) { wagonsQ.Enqueue(coupledFront); } // Relies on having already removed the current car to avoid cycles/infinite loop

				TrainCar? coupledRear = _wagons.FirstOrDefault(wagon => wagon == currentWagon.rearCoupler?.coupledTo?.train);
				if (coupledRear != null) { wagonsQ.Enqueue(coupledRear); } // Relies on having already removed the current car to avoid cycles/infinite loop
			}

			if (coupled.Count > 0)
			{
				coupledSets.Add(new CoupledSetData(coupled, coupledLength));
			}
			else
			{
				Main.LogError("Coupled set was empty. This should never happen!");
			}
		}

		Main.LogDebug(() => $"Grouped cars by coupled: {WagonGroupsToString(coupledSets.Select(coupledSet => coupledSet.Wagons))}");

		return coupledSets;
	}

	private static TrainCar? GetNextEdgeWagonFromPool(IEnumerable<TrainCar> wagonPool)
	{
		TrainCar? currentWagon = wagonPool.FirstOrDefault();
		TrainCar? previousWagon = currentWagon;
		while (currentWagon != null)
		{
			previousWagon = currentWagon;
			currentWagon = wagonPool.FirstOrDefault(wagon => wagon == previousWagon.frontCoupler?.coupledTo?.train);
		}
		return previousWagon;
	}

	private static string WagonGroupsToString(IEnumerable<IEnumerable<TrainCar>> wagonGroups)
	{
		return $"[{string.Join(", ", wagonGroups.Select(WagonGroupToString))}]";
	}

	private static string WagonGroupToString(IEnumerable<TrainCar> wagons)
	{
		return $"[{string.Join(", ", wagons.Select(wagon => wagon.ID))}]";
	}

	private static double GetLongestTrackLength(IEnumerable<Track> tracks)
	{
		return tracks.Aggregate(0d, (maxLength, track) => {
			double trackLength = track.length;
			if (trackLength > maxLength)
			{
				return trackLength;
			}
			return maxLength;
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
