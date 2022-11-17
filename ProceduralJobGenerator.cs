using DV.Logic.Job;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVOwnership
{
    public class ProceduralJobGenerator
    {
        private StationController stationController;
        private StationProceduralJobsRuleset generationRuleset;
        private YardTracksOrganizer yto;

        public ProceduralJobGenerator(StationController stationController)
        {
            this.stationController = stationController;
            generationRuleset = stationController.proceduralJobsRuleset;
            yto = SingletonBehaviour<YardTracksOrganizer>.Instance;
        }

        public JobChainController GenerateHaulChainJobForCars(System.Random rng, List<Car> carsForJob, CargoGroup cargoGroup)
        {
            List<CargoType> cargoTypes = (from car in carsForJob select car.CurrentCargoTypeInCar).ToList();
            List<float> cargoAmounts = (from car in carsForJob select car.LoadedCargoAmount).ToList();

            var tracksForCars = (from car in carsForJob select car.CurrentTrack).ToHashSet();
            if (tracksForCars.Count != 1)
            {
                DVOwnership.LogError($"Expected only one starting track for {JobType.Transport} job, but got {tracksForCars.Count}.");
                return null;
            }
            var startingTrack = tracksForCars.First();

            float approxLengthOfWholeTrain = yto.GetTotalCarsLength(carsForJob) + yto.GetSeparationLengthBetweenCars(carsForJob.Count);

            JobLicenses jobLicenses = LicenseManager.GetRequiredLicensesForCargoTypes(cargoTypes) | LicenseManager.GetRequiredLicenseForNumberOfTransportedCars(carsForJob.Count);

            var destinationController = Utilities.GetRandomFrom(rng, cargoGroup.stations);
            var possibleDestinationTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(destinationController.logicStation.yard.TransferInTracks, approxLengthOfWholeTrain);
            if (possibleDestinationTracks.Count < 1)
            {
                DVOwnership.LogWarning($"Station[{stationController.logicStation.ID}] couldn't find a destination track with enough free space for the job. ({approxLengthOfWholeTrain})");
                return null;
            }
            var destinationTrack = Utilities.GetRandomFrom(rng, possibleDestinationTracks);

            var gameObject = new GameObject($"ChainJob[{JobType.Transport}]: {stationController.logicStation.ID} - {destinationController.logicStation.ID}");
            gameObject.transform.SetParent(stationController.transform);
            var jobChainController = new JobChainController(gameObject);

            var stationsChainData = new StationsChainData(stationController.stationInfo.YardID, destinationController.stationInfo.YardID);

            float distanceBetweenStations = JobPaymentCalculator.GetDistanceBetweenStations(stationController, destinationController);
            float bonusTimeLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(distanceBetweenStations);
            float baseWage = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, distanceBetweenStations, Utilities.ExtractPaymentCalculationData(carsForJob));
            JobLicenses requiredLicenses = jobLicenses | LicenseManager.GetRequiredLicensesForJobType(JobType.Transport);

            var jobDefinition = PopulateHaulJobDefinitionWithExistingCars(jobChainController.jobChainGO, stationController.logicStation, startingTrack, destinationTrack, carsForJob, cargoTypes, cargoAmounts, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);

            jobChainController.AddJobDefinitionToChain(jobDefinition);
            jobChainController.FinalizeSetupAndGenerateFirstJob();

            jobChainController.JobChainCompleted += GenerateDestinationSetter(destinationController.logicStation.ID);

            return jobChainController;
        }

        public JobChainController GenerateUnloadChainJobForCars(System.Random rng, List<Car> carsForJob, CargoGroup cargoGroup)
        {
            List<CargoType> cargoTypes = (from car in carsForJob select car.CurrentCargoTypeInCar).ToList();

            var tracksForCars = (from car in carsForJob select car.CurrentTrack).ToHashSet();
            if (tracksForCars.Count != 1)
            {
                DVOwnership.LogError($"Expected only one starting track for {JobType.ShuntingUnload} job, but got {tracksForCars.Count}.");
                return null;
            }
            var startingTrack = tracksForCars.First();

            float approxLengthOfWholeTrain = yto.GetTotalCarsLength(carsForJob) + yto.GetSeparationLengthBetweenCars(carsForJob.Count);

            JobLicenses jobLicenses = LicenseManager.GetRequiredLicensesForCargoTypes(cargoTypes) | LicenseManager.GetRequiredLicenseForNumberOfTransportedCars(carsForJob.Count);

            var warehouseMachinesThatSupportCargoTypes = stationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(cargoTypes);
            if (warehouseMachinesThatSupportCargoTypes.Count == 0)
            {
                DVOwnership.LogError($"Station[{stationController.logicStation.ID}] doesn't have a warehouse machine that supports all cargo types for the job. [{cargoTypes.Aggregate("", (str, type) => $"{str}, {type}")}]");
                return null;
            }
            warehouseMachinesThatSupportCargoTypes.RemoveAll((machine) => machine.WarehouseTrack.length < (double)approxLengthOfWholeTrain);
            if (warehouseMachinesThatSupportCargoTypes.Count == 0)
            {
                DVOwnership.LogDebug(() => $"Station[{stationController.logicStation.ID}] doesn't have a warehouse track long enough for the job. ({approxLengthOfWholeTrain})");
                return null;
            }
            var warehouseMachine = Utilities.GetRandomFrom(rng, warehouseMachinesThatSupportCargoTypes);

            var randomSortingOfCarsOnTracks = Utilities.GetRandomSortingOfCarsOnTracks(rng, stationController.logicStation.yard.StorageTracks, carsForJob, generationRuleset.maxShuntingStorageTracks);
            if (randomSortingOfCarsOnTracks == null)
            {
                DVOwnership.LogDebug(() => $"Station[{stationController.logicStation.ID}] couldn't assign cars to storage tracks.");
                return null;
            }

            var originController = Utilities.GetRandomFrom(rng, cargoGroup.stations);

            var gameObject = new GameObject($"ChainJob[{JobType.ShuntingUnload}]: {originController.logicStation.ID} - {stationController.logicStation.ID}");
            gameObject.transform.SetParent(stationController.transform);
            var jobChainController = new JobChainController(gameObject);

            var stationsChainData = new StationsChainData(originController.stationInfo.YardID, stationController.stationInfo.YardID);

            int countTracks = randomSortingOfCarsOnTracks.Count;
            float bonusTimeLimit = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(countTracks);
            float distanceInMeters = 500f * countTracks;
            float baseWage = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingUnload, distanceInMeters, Utilities.ExtractPaymentCalculationData(carsForJob));
            JobLicenses requiredLicenses = jobLicenses | LicenseManager.GetRequiredLicensesForJobType(JobType.ShuntingUnload);

            var carsPerCargoType = Utilities.ExtractCarsPerCargoType(carsForJob);

            var jobDefinition = PopulateShuntingUnloadJobDefinitionWithExistingCars(jobChainController.jobChainGO, stationController.logicStation, startingTrack, warehouseMachine, carsPerCargoType, randomSortingOfCarsOnTracks, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);

            jobChainController.AddJobDefinitionToChain(jobDefinition);
            jobChainController.FinalizeSetupAndGenerateFirstJob();

            jobChainController.JobChainCompleted += GenerateDestinationSetter(null);

            return jobChainController;
        }

        public JobChainController GenerateLoadChainJobForCars(System.Random rng, List<List<Car>> carSetsForJob, CargoGroup cargoGroup)
        {
            List<CarsPerTrack> carsPerStartingTrack = new List<CarsPerTrack>();
            List<Car> carsForJob = new List<Car>();
            List<CargoType> cargoTypes = new List<CargoType>();
            foreach (var carSet in carSetsForJob)
            {
                var track = carSet.Aggregate(null as Track, (t, c) =>
                {
                    if (t == null && c.FrontBogieTrack == c.RearBogieTrack) { return c.FrontBogieTrack; }
                    return t;
                });
                if (track == null)
                {
                    track = carSet[0].FrontBogieTrack;
                    DVOwnership.LogWarning($"Station[{stationController.logicStation.ID}] couldn't determine track of cars for shunting load job. Using track {track} in order to continue.");
                }
                var carsPerTrack = new CarsPerTrack(track, carSet);
                carsPerStartingTrack.Add(carsPerTrack);

                foreach (var car in carSet)
                {
                    carsForJob.Add(car);
                    var potentialCargoTypes = from cargoType in cargoGroup.cargoTypes
                                              where CargoTypes.CanCarContainCargoType(car.carType, cargoType)
                                              select cargoType;
                    if (potentialCargoTypes.Count() < 1)
                    {
                        DVOwnership.LogError($"Station[{stationController.logicStation.ID}] found no matching cargo types for car type {car.carType} and cargo group cargo types [{cargoGroup.cargoTypes.Aggregate("", (str, type) => $"{str}, {type}")}].");
                        return null;
                    }
                    var selectedCargoType = Utilities.GetRandomFrom(rng, potentialCargoTypes);
                    cargoTypes.Add(selectedCargoType);
                }
            }
            List<CarsPerCargoType> carsPerCargoTypes = Utilities.ExtractCarsPerCargoType(carsForJob, cargoTypes);

            float approxLengthOfWholeTrain = yto.GetTotalCarsLength(carsForJob) + yto.GetSeparationLengthBetweenCars(carsForJob.Count);

            JobLicenses jobLicenses = LicenseManager.GetRequiredLicensesForCargoTypes(cargoTypes) | LicenseManager.GetRequiredLicenseForNumberOfTransportedCars(carsForJob.Count);

            var warehouseMachinesThatSupportCargoTypes = stationController.logicStation.yard.GetWarehouseMachinesThatSupportCargoTypes(cargoTypes);
            if (warehouseMachinesThatSupportCargoTypes.Count == 0)
            {
                DVOwnership.LogError($"Station[{stationController.logicStation.ID}] doesn't have a warehouse machine that supports all cargo types for the job. [{cargoTypes.Aggregate("", (str, type) => $"{str}, {type}")}]");
                return null;
            }
            warehouseMachinesThatSupportCargoTypes.RemoveAll((machine) => machine.WarehouseTrack.length < (double)approxLengthOfWholeTrain);
            if (warehouseMachinesThatSupportCargoTypes.Count == 0)
            {
                DVOwnership.LogDebug(() => $"Station[{stationController.logicStation.ID}] doesn't have a warehouse track long enough for the job. ({approxLengthOfWholeTrain})");
                return null;
            }
            var warehouseMachine = Utilities.GetRandomFrom(rng, warehouseMachinesThatSupportCargoTypes);

            var destinationController = Utilities.GetRandomFrom(rng, cargoGroup.stations);
            var possibleDestinationTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(stationController.logicStation.yard.StorageTracks, approxLengthOfWholeTrain);
            if (possibleDestinationTracks.Count < 1)
            {
                DVOwnership.LogWarning($"Station[{stationController.logicStation.ID}] couldn't find a storage track with enough free space for the job. ({approxLengthOfWholeTrain})");
                return null;
            }
            var destinationTrack = Utilities.GetRandomFrom(rng, possibleDestinationTracks);

            var gameObject = new GameObject($"ChainJob[{JobType.ShuntingLoad}]: {stationController.logicStation.ID} - {destinationController.logicStation.ID}");
            gameObject.transform.SetParent(stationController.transform);
            var jobChainController = new JobChainController(gameObject);

            var stationsChainData = new StationsChainData(stationController.stationInfo.YardID, destinationController.stationInfo.YardID);

            int countTracks = carsPerStartingTrack.Count;
            float bonusTimeLimit = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(countTracks);
            float distanceInMeters = 500f * countTracks;
            float baseWage = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingLoad, distanceInMeters, Utilities.ExtractPaymentCalculationData(carsForJob));
            JobLicenses requiredLicenses = jobLicenses | LicenseManager.GetRequiredLicensesForJobType(JobType.ShuntingLoad);

            var jobDefinition = PopulateShuntingLoadJobDefinitionWithExistingCars(jobChainController.jobChainGO, stationController.logicStation, carsPerStartingTrack, warehouseMachine, carsPerCargoTypes, destinationTrack, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);

            jobChainController.AddJobDefinitionToChain(jobDefinition);
            jobChainController.FinalizeSetupAndGenerateFirstJob();

            return jobChainController;
        }

        private static StaticTransportJobDefinition PopulateHaulJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, Track startingTrack, Track destinationTrack, List<Car> logicCarsToHaul, List<CargoType> cargoTypePerCar, List<float> cargoAmountPerCar, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
        {
            var jobDefinition = chainJobGO.AddComponent<StaticTransportJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
            jobDefinition.startingTrack = startingTrack;
            jobDefinition.trainCarsToTransport = logicCarsToHaul;
            jobDefinition.transportedCargoPerCar = cargoTypePerCar;
            jobDefinition.cargoAmountPerCar = cargoAmountPerCar;
            jobDefinition.forceCorrectCargoStateOnCars = true;
            jobDefinition.destinationTrack = destinationTrack;
            return jobDefinition;
        }

        private static StaticShuntingUnloadJobDefinition PopulateShuntingUnloadJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, Track startingTrack, WarehouseMachine unloadMachine, List<CarsPerCargoType> carsPerCargoType, List<CarsPerTrack> carsPerDestinationTrack, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
        {
            var jobDefinition = chainJobGO.AddComponent<StaticShuntingUnloadJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
            jobDefinition.startingTrack = startingTrack;
            jobDefinition.unloadMachine = unloadMachine;
            jobDefinition.unloadData = carsPerCargoType;
            jobDefinition.carsPerDestinationTrack = carsPerDestinationTrack;
            jobDefinition.forceCorrectCargoStateOnCars = true;
            return jobDefinition;
        }

        private static StaticShuntingLoadJobDefinition PopulateShuntingLoadJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, List<CarsPerTrack> carsPerStartingTrack, WarehouseMachine loadMachine, List<CarsPerCargoType> carsPerCargoType, Track destinationTrack, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
        {
            var jobDefinition = chainJobGO.AddComponent<StaticShuntingLoadJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
            jobDefinition.carsPerStartingTrack = carsPerStartingTrack;
            jobDefinition.loadMachine = loadMachine;
            jobDefinition.loadData = carsPerCargoType;
            jobDefinition.destinationTrack = destinationTrack;
            jobDefinition.forceCorrectCargoStateOnCars = true;
            return jobDefinition;
        }

        private static Action<JobChainController> GenerateDestinationSetter(string destinationID)
        {
            return (controller) =>
            {
                var rollingStock = SingletonBehaviour<RollingStockManager>.Instance;
                var trainCars = controller.trainCarsForJobChain;
                var equipments = from trainCar in trainCars select rollingStock.FindByTrainCar(trainCar);
                foreach (var equipment in equipments)
                {
                    equipment.SetDestination(destinationID);
                }
            };
        }
    }
}
