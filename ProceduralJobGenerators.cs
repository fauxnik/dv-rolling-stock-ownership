using DV.Logic.Job;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVOwnership
{
	public static class ProceduralJobGenerators
	{
		public static JobChainController? GenerateHaulChainJobForCars(System.Random rng, List<Car> carsForJob, CargoGroup cargoGroup, StationController originController)
		{
			return GenerateHaulChainJobForCars(rng, carsForJob, originController, Utilities.GetRandomFrom(rng, cargoGroup.stations));
		}

		private static JobChainController? GenerateHaulChainJobForCars(System.Random rng, List<Car> carsForJob, StationController originController, StationController destinationController)
		{
			var yto = YardTracksOrganizer.Instance;
			var carSpawn = CarSpawner.Instance;
			var licenseManager = LicenseManager.Instance;

			List<CargoType> cargoTypes = (from car in carsForJob select car.CurrentCargoTypeInCar).ToList();
			List<float> cargoAmounts = (from car in carsForJob select car.LoadedCargoAmount).ToList();

			HashSet<Track> tracksForCars = (from car in carsForJob select car.CurrentTrack).ToHashSet();
			if (tracksForCars.Count != 1)
			{
				DVOwnership.LogError($"Expected only one starting track for {JobType.Transport} job, but got {tracksForCars.Count}.");
				return null;
			}
			Track startingTrack = tracksForCars.First();

			float approxLengthOfWholeTrain = carSpawn.GetTotalCarsLength(carsForJob) + carSpawn.GetSeparationLengthBetweenCars(carsForJob.Count);

			HashSet<JobLicenseType_v2> jobLicenses = new (
				from license in licenseManager.GetRequiredLicensesForCargoTypes(cargoTypes)
					.Append(licenseManager.GetRequiredLicenseForNumberOfTransportedCars(carsForJob.Count))
				where license != null
				select license
			);

			List<Track> possibleDestinationTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(destinationController.logicStation.yard.TransferInTracks, approxLengthOfWholeTrain);
			if (possibleDestinationTracks.Count < 1)
			{
				DVOwnership.LogWarning($"Station[{originController.logicStation.ID}] couldn't find an inbound track with enough free space for the job. ({approxLengthOfWholeTrain})");
				return null;
			}
			Track destinationTrack = Utilities.GetRandomFrom(rng, possibleDestinationTracks);

			var gameObject = new GameObject($"ChainJob[{JobType.Transport}]: {originController.logicStation.ID} - {destinationController.logicStation.ID}");
			gameObject.transform.SetParent(originController.transform);
			// This class is patched to do next-in-chain job generation
			var jobChainController = new JobChainControllerWithEmptyHaulGeneration(gameObject)
			{
				trainCarsForJobChain = Utilities.ConvertLogicCarsToTrainCars(carsForJob).ToList()
			};

			var stationsChainData = new StationsChainData(originController.stationInfo.YardID, destinationController.stationInfo.YardID);

			float distanceBetweenStations = JobPaymentCalculator.GetDistanceBetweenStations(originController, destinationController);
			float bonusTimeLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(distanceBetweenStations);
			float baseWage = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, distanceBetweenStations, Utilities.ExtractPaymentCalculationData(carsForJob));
			HashSet<JobLicenseType_v2> requiredLicenses = jobLicenses.Concat(licenseManager.GetRequiredLicensesForJobType(JobType.Transport)).ToHashSet();

			StaticTransportJobDefinition jobDefinition = PopulateHaulJobDefinitionWithExistingCars(jobChainController.jobChainGO, originController.logicStation, startingTrack, destinationTrack, carsForJob, cargoTypes, cargoAmounts, bonusTimeLimit, baseWage, stationsChainData, JobLicenseType_v2.ListToFlags(requiredLicenses));

			jobChainController.AddJobDefinitionToChain(jobDefinition);
			jobChainController.FinalizeSetupAndGenerateFirstJob();

			return jobChainController;
		}

		public static JobChainController? GenerateUnloadChainJobForCars(System.Random rng, List<Car> carsForJob, CargoGroup cargoGroup, StationController destinationController)
		{
			return GenerateUnloadChainJobForCars(rng, carsForJob, Utilities.GetRandomFrom(rng, cargoGroup.stations), destinationController);
		}

		private static JobChainController? GenerateUnloadChainJobForCars(System.Random rng, List<Car> carsForJob, StationController originController, StationController destinationController)
		{
			var yto = YardTracksOrganizer.Instance;
			var carSpawn = CarSpawner.Instance;
			var licenseManager = LicenseManager.Instance;
			StationProceduralJobsRuleset generationRuleset = destinationController.proceduralJobsRuleset;

			List<CargoType> cargoTypes = (from car in carsForJob select car.CurrentCargoTypeInCar).ToList();

			HashSet<Track> tracksForCars = (from car in carsForJob select car.CurrentTrack).ToHashSet();
			if (tracksForCars.Count != 1)
			{
				DVOwnership.LogError($"Expected only one starting track for {JobType.ShuntingUnload} job, but got {tracksForCars.Count}.");
				return null;
			}
			Track startingTrack = tracksForCars.First();

			float approxLengthOfWholeTrain = carSpawn.GetTotalCarsLength(carsForJob) + carSpawn.GetSeparationLengthBetweenCars(carsForJob.Count);

			HashSet<JobLicenseType_v2> jobLicenses = new (
				from license in licenseManager.GetRequiredLicensesForCargoTypes(cargoTypes)
					.Append(licenseManager.GetRequiredLicenseForNumberOfTransportedCars(carsForJob.Count))
				where license != null
				select license
			);

			List<WarehouseMachine>? warehouseMachinesThatSupportCargoTypes = destinationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(cargoTypes);
			if (warehouseMachinesThatSupportCargoTypes.Count == 0)
			{
				DVOwnership.LogError($"Station[{destinationController.logicStation.ID}] doesn't have a warehouse machine that supports all cargo types for the job. [{string.Join(", ", cargoTypes)}]");
				return null;
			}
			warehouseMachinesThatSupportCargoTypes.RemoveAll((machine) => machine.WarehouseTrack.length < (double)approxLengthOfWholeTrain);
			if (warehouseMachinesThatSupportCargoTypes.Count == 0)
			{
				DVOwnership.LogDebug(() => $"Station[{destinationController.logicStation.ID}] doesn't have a warehouse track long enough for the job. ({approxLengthOfWholeTrain})");
				return null;
			}
			WarehouseMachine warehouseMachine = Utilities.GetRandomFrom(rng, warehouseMachinesThatSupportCargoTypes);

			List<CarsPerTrack>? randomSortingOfCarsOnTracks = Utilities.GetRandomSortingOfCarsOnTracks(rng, destinationController.logicStation.yard.StorageTracks, carsForJob, generationRuleset.maxShuntingStorageTracks, generationRuleset.minCarsPerJob);
			if (randomSortingOfCarsOnTracks == null)
			{
				DVOwnership.LogDebug(() => $"Station[{destinationController.logicStation.ID}] couldn't assign cars to storage tracks.");
				return null;
			}

			var gameObject = new GameObject($"ChainJob[{JobType.ShuntingUnload}]: {originController.logicStation.ID} - {destinationController.logicStation.ID}");
			gameObject.transform.SetParent(destinationController.transform);
			// This class is patched to do next-in-chain job generation
			var jobChainController = new JobChainControllerWithEmptyHaulGeneration(gameObject)
			{
				trainCarsForJobChain = Utilities.ConvertLogicCarsToTrainCars(carsForJob).ToList()
			};

			var stationsChainData = new StationsChainData(originController.stationInfo.YardID, destinationController.stationInfo.YardID);

			int countTracks = randomSortingOfCarsOnTracks.Count;
			float bonusTimeLimit = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(countTracks);
			float distanceInMeters = 500f * countTracks;
			float baseWage = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingUnload, distanceInMeters, Utilities.ExtractPaymentCalculationData(carsForJob));
			HashSet<JobLicenseType_v2> requiredLicenses = jobLicenses.Concat(licenseManager.GetRequiredLicensesForJobType(JobType.ShuntingUnload)).ToHashSet();

			List<CarsPerCargoType> carsPerCargoType = Utilities.ExtractCarsPerCargoType(carsForJob);

			StaticShuntingUnloadJobDefinition jobDefinition = PopulateShuntingUnloadJobDefinitionWithExistingCars(jobChainController.jobChainGO, destinationController.logicStation, startingTrack, warehouseMachine, carsPerCargoType, randomSortingOfCarsOnTracks, bonusTimeLimit, baseWage, stationsChainData, JobLicenseType_v2.ListToFlags(requiredLicenses));

			jobChainController.AddJobDefinitionToChain(jobDefinition);
			jobChainController.FinalizeSetupAndGenerateFirstJob();

			return jobChainController;
		}

		public static JobChainController? GenerateLoadChainJobForCars(System.Random rng, List<List<Car>> carSetsForJob, CargoGroup cargoGroup, StationController originController)
		{

			var yto = YardTracksOrganizer.Instance;
			var carSpawn = CarSpawner.Instance;
			var licenseManager = LicenseManager.Instance;

			var carsPerStartingTrack = new List<CarsPerTrack>();
			var carsForJob = new List<Car>();
			var cargoTypes = new List<CargoType>();
			var cargoTypes_v2 = new List<CargoType_v2>();
			foreach (CargoType cargotype in cargoGroup.cargoTypes)
			{
				DVOwnership.LogDebug(() => $"Cargo : {cargotype}");
				cargoTypes_v2.Add(TransitionHelpers.ToV2(cargotype));
			}

			foreach (List<Car> carSet in carSetsForJob)
			{
				Track? track = carSet.Aggregate(null as Track, (t, c) =>
				{
					if (t == null && c.FrontBogieTrack == c.RearBogieTrack) { return c.FrontBogieTrack; }
					return t;
				});
				if (track == null)
				{
					track = carSet[0].FrontBogieTrack;
					DVOwnership.LogWarning($"Station[{originController.logicStation.ID}] couldn't determine track of cars for shunting load job. Using track {track} in order to continue.");
				}
				var carsPerTrack = new CarsPerTrack(track, carSet);
				carsPerStartingTrack.Add(carsPerTrack);

				foreach (Car car in carSet)
				{
					carsForJob.Add(car);
					var potentialCargoTypes = from cargoType_v2 in cargoTypes_v2
					                          where cargoType_v2.IsLoadableOnCarType(car.carType.parentType)
					                          select cargoType_v2;
					if (potentialCargoTypes.Count() < 1)
					{
						DVOwnership.LogError($"Station[{originController.logicStation.ID}] found no matching cargo types for car type {car.carType} and cargo group cargo types [{string.Join(", ", cargoGroup.cargoTypes)}].");
						return null;
					}
					CargoType_v2 selectedCargoType = Utilities.GetRandomFrom(rng, potentialCargoTypes);
					cargoTypes.Add(selectedCargoType.v1);
				}
			}
			List<CarsPerCargoType> carsPerCargoTypes = Utilities.ExtractCarsPerCargoType(carsForJob, cargoTypes);

			float approxLengthOfWholeTrain = carSpawn.GetTotalCarsLength(carsForJob) + carSpawn.GetSeparationLengthBetweenCars(carsForJob.Count);

			HashSet<JobLicenseType_v2> jobLicenses = new (
				from license in licenseManager.GetRequiredLicensesForCargoTypes(cargoTypes)
					.Append(licenseManager.GetRequiredLicenseForNumberOfTransportedCars(carsForJob.Count))
				where license != null
				select license
			);

			List<WarehouseMachine> warehouseMachinesThatSupportCargoTypes = originController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(cargoTypes);
			if (warehouseMachinesThatSupportCargoTypes.Count == 0)
			{
				DVOwnership.LogError($"Station[{originController.logicStation.ID}] doesn't have a warehouse machine that supports all cargo types for the job. [{string.Join(", ", cargoTypes)}]");
				return null;
			}
			warehouseMachinesThatSupportCargoTypes.RemoveAll((machine) => machine.WarehouseTrack.length < (double)approxLengthOfWholeTrain);
			if (warehouseMachinesThatSupportCargoTypes.Count == 0)
			{
				DVOwnership.LogDebug(() => $"Station[{originController.logicStation.ID}] doesn't have a warehouse track long enough for the job. ({approxLengthOfWholeTrain})");
				return null;
			}
			WarehouseMachine warehouseMachine = Utilities.GetRandomFrom(rng, warehouseMachinesThatSupportCargoTypes);

			StationController destinationController = Utilities.GetRandomFrom(rng, cargoGroup.stations);
			List<Track> possibleDestinationTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(originController.logicStation.yard.TransferOutTracks, approxLengthOfWholeTrain);
			if (possibleDestinationTracks.Count() < 1)
			{
				DVOwnership.LogWarning($"Station[{originController.logicStation.ID}] couldn't find an outbound track with enough free space for the job. ({approxLengthOfWholeTrain})");
				return null;
			}
			Track destinationTrack = Utilities.GetRandomFrom(rng, possibleDestinationTracks);

			var gameObject = new GameObject($"ChainJob[{JobType.ShuntingLoad}]: {originController.logicStation.ID} - {destinationController.logicStation.ID}");
			gameObject.transform.SetParent(originController.transform);
			// This class is patched to do next-in-chain job generation
			var jobChainController = new JobChainControllerWithEmptyHaulGeneration(gameObject)
			{
				trainCarsForJobChain = Utilities.ConvertLogicCarsToTrainCars(carsForJob).ToList()
			};

			var stationsChainData = new StationsChainData(originController.stationInfo.YardID, destinationController.stationInfo.YardID);

			int countTracks = carsPerStartingTrack.Count;
			float bonusTimeLimit = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(countTracks);
			float distanceInMeters = 500f * countTracks;
			float baseWage = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingLoad, distanceInMeters, Utilities.ExtractPaymentCalculationData(carsForJob));
			HashSet<JobLicenseType_v2> requiredLicenses = jobLicenses.Concat(licenseManager.GetRequiredLicensesForJobType(JobType.ShuntingLoad)).ToHashSet();

			StaticShuntingLoadJobDefinition jobDefinition = PopulateShuntingLoadJobDefinitionWithExistingCars(jobChainController.jobChainGO, originController.logicStation, carsPerStartingTrack, warehouseMachine, carsPerCargoTypes, destinationTrack, bonusTimeLimit, baseWage, stationsChainData, JobLicenseType_v2.ListToFlags(requiredLicenses));

			jobChainController.AddJobDefinitionToChain(jobDefinition);
			jobChainController.FinalizeSetupAndGenerateFirstJob();

			return jobChainController;
		}

		private static StaticTransportJobDefinition PopulateHaulJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, Track startingTrack, Track destinationTrack, List<Car> logicCarsToHaul, List<CargoType> cargoTypePerCar, List<float> cargoAmountPerCar, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
		{
			if (chainJobGO == null) { DVOwnership.LogError("Chain job game object must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (logicStation == null) { DVOwnership.LogError("Origin station must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (startingTrack == null) { DVOwnership.LogError("Starting track must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (destinationTrack == null) { DVOwnership.LogError("Destination track must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (logicCarsToHaul == null) { DVOwnership.LogError("List of logic cars must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (logicCarsToHaul.Any(car => car == null)) { DVOwnership.LogError("All logic cars must not be null, but at least one of them is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (cargoTypePerCar == null) { DVOwnership.LogError("List of cargo type per car must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (cargoAmountPerCar == null) { DVOwnership.LogError("List of cargo amounts must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			if (stationsChainData == null) { DVOwnership.LogError("Stations chain data must not be null, but is (in PopulateHaulJobDefinitionWithExistingCars)"); }
			var jobDefinition = chainJobGO!.AddComponent<StaticTransportJobDefinition>();
			jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
			jobDefinition.startingTrack = startingTrack;
			jobDefinition.trainCarsToTransport = logicCarsToHaul;
			jobDefinition.transportedCargoPerCar = cargoTypePerCar;
			jobDefinition.cargoAmountPerCar = cargoAmountPerCar;
			jobDefinition.forceCorrectCargoStateOnCars = true;
			jobDefinition.destinationTrack = destinationTrack;
			return jobDefinition;
		}

		private static StaticShuntingUnloadJobDefinition PopulateShuntingUnloadJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, Track startingTrack, WarehouseMachine unloadMachine, List<CarsPerCargoType> carsPerCargoTypes, List<CarsPerTrack> carsPerDestinationTrack, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
		{
			if (chainJobGO == null) { DVOwnership.LogError("Chain job game object must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (logicStation == null) { DVOwnership.LogError("Origin station must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (startingTrack == null) { DVOwnership.LogError("Starting track must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (unloadMachine == null) { DVOwnership.LogError("Warehouse machine must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes == null) { DVOwnership.LogError("List of cars per cargo types must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes.Any(carsPerCargoType => carsPerCargoType == null)) { DVOwnership.LogError("All cars per cargo type must not be null, but at least one of them is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes.Any(carsPerCargoType => carsPerCargoType.cars == null)) { DVOwnership.LogError("All lists of cars must not be null, but at least one of them is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes.Any(carsPerCargoType => carsPerCargoType.cars.Any(car => car == null))) { DVOwnership.LogError("All cars must not be null, but at least one of them is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (carsPerDestinationTrack == null) { DVOwnership.LogError("List of cars per destination track must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (carsPerDestinationTrack.Any(carsPerTrack => carsPerTrack == null)) { DVOwnership.LogError("All cars per destination track must not be null, but at least one of them is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			if (stationsChainData == null) { DVOwnership.LogError("Stations chain data must not be null, but is (in PopulateShuntingUnloadJobDefinitionWithExistingCars)"); }
			var jobDefinition = chainJobGO!.AddComponent<StaticShuntingUnloadJobDefinition>();
			jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
			jobDefinition.startingTrack = startingTrack;
			jobDefinition.unloadMachine = unloadMachine;
			jobDefinition.unloadData = carsPerCargoTypes;
			jobDefinition.carsPerDestinationTrack = carsPerDestinationTrack;
			jobDefinition.forceCorrectCargoStateOnCars = true;
			return jobDefinition;
		}

		private static StaticShuntingLoadJobDefinition PopulateShuntingLoadJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, List<CarsPerTrack> carsPerStartingTrack, WarehouseMachine loadMachine, List<CarsPerCargoType> carsPerCargoTypes, Track destinationTrack, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
		{
			if (chainJobGO == null) { DVOwnership.LogError("Chain job game object must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (logicStation == null) { DVOwnership.LogError("Origin station must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (carsPerStartingTrack == null) { DVOwnership.LogError("List of cars per starting track must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (carsPerStartingTrack.Any(carsPerTrack => carsPerTrack == null)) { DVOwnership.LogError("All cars per starting track must not be null, but at least one of them is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (loadMachine == null) { DVOwnership.LogError("Warehouse machine must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes == null) { DVOwnership.LogError("List of cars per cargo types must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes.Any(carsPerCargoType => carsPerCargoType == null)) { DVOwnership.LogError("All cars per cargo type must not be null, but at least one of them is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes.Any(carsPerCargoType => carsPerCargoType.cars == null)) { DVOwnership.LogError("All lists of cars must not be null, but at least one of them is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (carsPerCargoTypes.Any(carsPerCargoType => carsPerCargoType.cars.Any(car => car == null))) { DVOwnership.LogError("All cars must not be null, but at least one of them is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (destinationTrack == null) { DVOwnership.LogError("Destination track must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			if (stationsChainData == null) { DVOwnership.LogError("Stations chain data must not be null, but is (in PopulateShungingLoadJobDefinitionWithExistingCars)"); }
			var jobDefinition = chainJobGO!.AddComponent<StaticShuntingLoadJobDefinition>();
			jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
			jobDefinition.carsPerStartingTrack = carsPerStartingTrack;
			jobDefinition.loadMachine = loadMachine;
			jobDefinition.loadData = carsPerCargoTypes;
			jobDefinition.destinationTrack = destinationTrack;
			jobDefinition.forceCorrectCargoStateOnCars = true;
			return jobDefinition;
		}

		public static void SetDestination(JobChainController controller, string? destinationID)
		{
			var rollingStock = RollingStockManager.Instance;
			var trainCars = controller.trainCarsForJobChain;
			var equipments = from trainCar in trainCars select rollingStock.FindByTrainCar(trainCar);
			foreach (var equipment in equipments)
			{
				equipment.SetDestination(destinationID);
			}
		}

		public static void GenerateContinuationTransportJob(JobChainController? jobChainController, StationController originController, StationController destinationController)
		{
			var trainCars = jobChainController?.trainCarsForJobChain;
			if (trainCars == null)
			{
				DVOwnership.LogError($"Expected trainCarsForJobChain to exist, but it does not. Can't generate transport job.");
				return;
			}

			int tickCount = Environment.TickCount;
			System.Random rng = new System.Random(tickCount);
			var carsForJob = from trainCar in trainCars select trainCar.logicCar;
			var completedJobID = jobChainController?.currentJobInChain?.ID;
			var originYardID = originController.stationInfo.YardID;
			var destinationYardID = destinationController.stationInfo.YardID;

			jobChainController = GenerateHaulChainJobForCars(rng, carsForJob.ToList(), originController, destinationController);

			if (jobChainController == null)
			{
				DVOwnership.LogError($"Couldn't generate a freight haul job to continue {completedJobID}!\n\torigin: {originYardID}\n\tdestination: {destinationYardID}\n\ttrain cars: {string.Join(", ", from trainCar in trainCars select trainCar.ID)}");
				return;
			}

			DVOwnership.Log($"Generated freight haul job {jobChainController.currentJobInChain.ID} as continuation of {completedJobID}.");
		}

		public static void GenerateContinuationUnloadJob(JobChainController? jobChainController, StationController originController, StationController destinationController)
		{
			var trainCars = jobChainController?.trainCarsForJobChain;
			if (trainCars == null)
			{
				DVOwnership.LogError($"Expected trainCarsForJobChain to exist, but it does not. Can't generate unload job.");
				return;
			}

			int tickCount = Environment.TickCount;
			System.Random rng = new System.Random(tickCount);
			var carsForJob = from trainCar in trainCars select trainCar.logicCar;
			var completedJobID = jobChainController?.currentJobInChain?.ID;
			var originYardID = originController.stationInfo.YardID;
			var destinationYardID = destinationController.stationInfo.YardID;

			jobChainController = GenerateUnloadChainJobForCars(rng, carsForJob.ToList(), originController, destinationController);

			if (jobChainController == null)
			{
				DVOwnership.LogError($"Couldn't generate an unload job to continue {completedJobID}!\n\torigin: {originYardID}\n\tdestination: {destinationYardID}\n\ttrain cars: {string.Join(", ", from trainCar in trainCars select trainCar.ID)}");
				return;
			}

			DVOwnership.Log($"Generated unload job {jobChainController.currentJobInChain.ID} as continuation of {completedJobID}.");
		}
	}
}
