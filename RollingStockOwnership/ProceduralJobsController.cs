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
		stationTracks = GetTracksByStationID(stationController.logicStation.ID).ToList();
	}

	public IEnumerator GenerateJobsCoro(Action onComplete, IEnumerable<Car>? carsToUse = null)
	{
		var log = new StringBuilder();
		int tickCount = Environment.TickCount;
		System.Random rng = new System.Random(tickCount);
		var stationId = stationController.logicStation.ID;
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

			Main.LogDebug(() => $"Found {carsInYard.Count()} cars in {stationId} yard and player's train.");

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

			Main.LogDebug(() => $"Found {carsInYard.Count()} jobless cars in {stationId} yard and player's train.");

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

				Main.LogDebug(() => $"Found {wagonsForLoading.Count} cars for shunting load jobs.");
				wagonsInYard.ExceptWith(wagonsForLoading);
			}
			else { Main.LogDebug(() => $"{stationId} doesn't support shunting load jobs."); }

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
					// TODO: check equipment reservations

					Main.LogDebug(() => $"Found {wagonsForHauling.Count} cars for transport jobs.");
					wagonsForHauling.Add(wagon);
				}

				wagonsInYard.ExceptWith(wagonsForHauling);
			}
			else { Main.LogDebug(() => $"{stationId} doesn't support transport jobs."); }

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
					// TODO: check equipment reservations

					Main.LogDebug(() => $"Found {wagonsForUnloading.Count} cars for shunting unload jobs.");
					wagonsForUnloading.Add(wagon);
				}

				wagonsInYard.ExceptWith(wagonsForUnloading);
			}
			else { Main.LogDebug(() => $"{stationId} doesn't support shunting unload jobs."); }

			Main.LogDebug(() => $"Excluding {wagonsInYard.Count} cars as incompatible.");

			/**
			 * SHUNTING LOAD
			 */
			int attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
			var loadingJobs = new List<JobChainController>();
			while (wagonsForLoading.Count > 0 && attemptsRemaining-- > 0)
			{
				// These are expensive operations, so we'll yield for a frame around them
				yield return null;
				Dictionary<CargoGroup, HashSet<TrainCar>> associations = AssociateWagonsWithCargoGroups(licensedOutboundCargoGroups, wagonsForLoading);
				yield return null;
				KeyValuePair<CargoGroup, HashSet<TrainCar>> association = ChooseAssociation(associations);
				yield return null;
				CargoGroup associatedCargoGroup = association.Key;
				HashSet<TrainCar> associatedWagons = association.Value;
				// This ensures a job will be generated even if there aren't technically enough total wagons
				int targetMinWagonsPerJob = associatedWagons.Count < 2 * stationMinWagonsPerJob ? associatedWagons.Count : stationMinWagonsPerJob;
				int absoluteMinWagonsPerJob = Math.Min(stationMinWagonsPerJob, associatedWagons.Count);
				yield return null;
				double maxWarehouseTrackLength = FindSupportedWarehouseMachineWithLongestTrack(
					associatedCargoGroup,
					stationController.warehouseMachineControllers
				).warehouseTrack.logicTrack.length;
				yield return null;
				IEnumerable<CoupledSetData> coupledSets = GroupWagonsByCoupled(associatedWagons, maxWagonsPerJob, maxWarehouseTrackLength).RandomSorting(rng);
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
					bool currentSliceSatisfiesMaxTrainLength = trainLength <= maxWarehouseTrackLength;

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
				JobChainController? jobChainController = ProceduralJobGenerators.GenerateLoadChainJobForCars(rng, carSetsForJob, associatedCargoGroup, stationController);

				if (jobChainController != null)
				{
					yield return null;
					// TODO: setup EquipmentReservation on job activation
					loadingJobs.Add(jobChainController);
					List<TrainCar> wagonsForJob = jobChainController.trainCarsForJobChain;
					wagonsForLoading.ExceptWith(wagonsForJob);
					Main.LogDebug(() => $"Generated shunting load job with cars {string.Join(", ", wagonsForJob.Select(tc => tc.ID))}.");
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
			{}
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

	private static IEnumerable<Track> GetTracksByStationID (string stationId)
	{
		var allTracks = RailTrackRegistry.Instance.AllTracks;
		return from railTrack in allTracks where railTrack.logicTrack.ID.yardId == stationId select railTrack.logicTrack;
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
	private static Dictionary<CargoGroup, HashSet<TrainCar>> AssociateWagonsWithCargoGroups(List<CargoGroup> cargoGroups, HashSet<TrainCar> wagons)
	{
		var associations = new Dictionary<CargoGroup, HashSet<TrainCar>>();

		foreach (CargoGroup cargoGroup in cargoGroups)
		{
			var association = associations[cargoGroup] = new HashSet<TrainCar>();

			foreach (CargoType cargoType in cargoGroup.cargoTypes)
			{
				foreach (TrainCar wagon in wagons)
				{
					if (cargoType.ToV2().IsLoadableOnCarType(wagon.logicCar.carType.parentType))
					{
						association.Add(wagon);
					}
				}
			}
		}

		return associations;
	}

	private static KeyValuePair<CargoGroup, HashSet<TrainCar>> ChooseAssociation(Dictionary<CargoGroup, HashSet<TrainCar>> associations)
	{
		var chosenAssociation = associations.Aggregate(null as KeyValuePair<CargoGroup, HashSet<TrainCar>>?, (accumulator, kvpair) => {
			int accumulatorSize = accumulator?.Value.Count ?? 0;
			int kvpairSize = kvpair.Value.Count;
			return kvpairSize > accumulatorSize ? kvpair : accumulator;
		});

		// All associations will exist in the input.
		// The nullable nature only comes from using null as the aggregate seed.
		return chosenAssociation!.Value;
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

	private static WarehouseMachineController FindSupportedWarehouseMachineWithLongestTrack(
		CargoGroup cargoGroup,
		IEnumerable<WarehouseMachineController> warehouseMachineControllers)
	{
		IEnumerable<WarehouseMachineController> supportedMachines =
			warehouseMachineControllers.Where(machine => machine.supportedCargoTypes.Intersect(cargoGroup.cargoTypes).Count() > 0);

		WarehouseMachineController supportedMachineWithLongestTrack = supportedMachines.Skip(1).Aggregate(
			supportedMachines.First(),
			(longest, current) => {
				double longestLength = longest.warehouseTrack.logicTrack.length;
				double currentLength = current.warehouseTrack.logicTrack.length;
				if (currentLength > longestLength)
				{
					return current;
				}
				return longest;
			}
		);

		return supportedMachineWithLongestTrack;
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
