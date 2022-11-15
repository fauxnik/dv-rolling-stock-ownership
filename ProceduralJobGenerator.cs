using DV.Logic.Job;
using System;
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

            throw new NotImplementedException();
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
