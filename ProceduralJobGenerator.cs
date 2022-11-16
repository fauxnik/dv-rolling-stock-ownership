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

            var destinationController = Utilities.GetRandomFromList(rng, cargoGroup.stations);
            var possibleDestinationTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(destinationController.logicStation.yard.TransferInTracks, approxLengthOfWholeTrain);
            if (possibleDestinationTracks.Count < 1)
            {
                DVOwnership.LogWarning($"Station[{stationController.logicStation.ID}] couldn't find a destination track with enough free space for the job. ({approxLengthOfWholeTrain})");
                return null;
            }
            var destinationTrack = Utilities.GetRandomFromList(rng, possibleDestinationTracks);

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
            var warehouseMachine = Utilities.GetRandomFromList(rng, warehouseMachinesThatSupportCargoTypes);

            var randomSortingOfCarsOnTracks = Utilities.GetRandomSortingOfCarsOnTracks(rng, stationController.logicStation.yard.StorageTracks, carsForJob, generationRuleset.maxShuntingStorageTracks);
            if (randomSortingOfCarsOnTracks == null)
            {
                DVOwnership.LogDebug(() => $"Station[{stationController.logicStation.ID}] couldn't assign cars to storage tracks.");
                return null;
            }

            var originController = Utilities.GetRandomFromList(rng, cargoGroup.stations);

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

            return jobChainController;
        }

        public JobChainController GenerateLoadChainJobForCars(System.Random rng, List<List<Car>> carSetsForJob, CargoGroup cargoGroup)
        {

            throw new NotImplementedException();
        }

        private StaticTransportJobDefinition PopulateHaulJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, Track startingTrack, Track destinationTrack, List<Car> logicCarsToHaul, List<CargoType> cargoTypePerCar, List<float> cargoAmountPerCar, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
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

        private StaticShuntingUnloadJobDefinition PopulateShuntingUnloadJobDefinitionWithExistingCars(GameObject chainJobGO, Station logicStation, Track startingTrack, WarehouseMachine unloadMachine, List<CarsPerCargoType> carsPerCargoType, List<CarsPerTrack> carsPerDestinationTrack, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
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
    }
}
