using DV.Logic.Job;
using DVOwnership.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DVOwnership
{
    public class ProceduralJobsController
    {
        private static readonly int MAX_JOB_GENERATION_ATTEMPTS = 30;

        private StationController stationController;
        private ProceduralJobGenerator procJobGenerator;
        private List<Track> stationTracks;

        public ProceduralJobsController(StationController stationController)
        {
            this.stationController = stationController;
            procJobGenerator = new ProceduralJobGenerator(stationController);
            stationTracks = GetTracksByStationID(stationController.logicStation.ID);
        }

        public IEnumerator GenerateJobsCoro()
        {
            var log = new StringBuilder();
            int tickCount = Environment.TickCount;
            System.Random rng = new System.Random(tickCount);
            var stationId = stationController.logicStation.ID;
            var proceduralRuleset = stationController.proceduralJobsRuleset;
            var licensedOutputCargoGroups = (from cargoGroup in proceduralRuleset.outputCargoGroups where LicenseManager_Patches.IsLicensedForCargoTypes(cargoGroup.cargoTypes) select cargoGroup).ToList();

            // get all (logic) cars in the yard
            var carsInYard = new HashSet<Car>();
            foreach (var track in stationTracks)
            {
                yield return null;

                foreach (var car in track.GetCarsFullyOnTrack())
                {
                    carsInYard.Add(car);
                }

                foreach (var car in track.GetCarsPartiallyOnTrack())
                {
                    carsInYard.Add(car);
                }
            }

            // TODO: get all (logic) cars from player's train

            // get all (logic) cars with active jobs
            var carsWithJobs = new HashSet<Car>();
            var activeJobs = PlayerJobs.Instance.currentJobs;
            foreach (var job in activeJobs)
            {
                yield return null;

                var taskQ = new Queue<Task>();
                foreach (var task in job.tasks)
                {
                    taskQ.Enqueue(task);
                }

                while (taskQ.Count > 0)
                {
                    var task = taskQ.Dequeue();
                    var taskData = task.GetTaskData();
                    var nestedTasks = taskData.nestedTasks;
                    foreach (var nestedTask in nestedTasks) { taskQ.Enqueue(nestedTask); }

                    if (taskData.type == TaskType.Transport)
                    {
                        foreach (var car in taskData.cars)
                        {
                            carsWithJobs.Add(car);
                        }
                    }
                }
            }

            // filter out (logic) cars with active jobs
            yield return null;
            carsInYard.ExceptWith(carsWithJobs);

            // loop, generating jobs for train cars, until all train cars are accounted for or we reach an upper bound of attempts
            var carsQ = new Queue<Car>();
            foreach (var car in carsInYard) { carsQ.Enqueue(car); }
            var attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
            while (attemptsRemaining > 0 && carsInYard.Count > 0 && carsQ.Count > 0)
            {
                yield return null;

                var thisCar = carsQ.Dequeue();
                if (!carsInYard.Contains(thisCar)) { continue; }

                DVOwnership.LogDebug(() => $"Attempting to generate job for car {thisCar.ID}.");

                // TODO: generate job
                JobChainController jobChainController = null;
                var manager = SingletonBehaviour<RollingStockManager>.Instance;
                var thisEquipment = manager.FindByCarGUID(thisCar.carGuid);
                var carsForJob = new HashSet<Car>();
                carsForJob.Add(thisCar);

                var carType = thisCar.carType;
                var cargoTypeInCar = thisCar.CurrentCargoTypeInCar;
                if (cargoTypeInCar != CargoType.None)
                {
                    if (proceduralRuleset.haulStartingJobSupported && licensedOutputCargoGroups.Any(group => group.cargoTypes.Contains(cargoTypeInCar)))
                    {
                        // Player previously loaded car here, generate freight haul job
                        var potentialCargoGroups = licensedOutputCargoGroups.Where(group => group.cargoTypes.Contains(cargoTypeInCar));
                        var countCargoGroups = potentialCargoGroups.Count();
                        var indexInCargoGroups = rng.Next(countCargoGroups);
                        var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

                        DVOwnership.LogDebug(() => $"Attempting to generate freight haul job using cargo group {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

                        // Move outward from car, seeking adjacent coupled cars that match the cargo group
                        var seekQ = new Queue<Equipment>();
                        var seenEquipment = new HashSet<Equipment>();
                        Equipment coupledEquipment;
                        coupledEquipment = manager.FindByCarGUID(thisEquipment.CarGuidCoupledFront);
                        if (coupledEquipment != null) { seekQ.Enqueue(coupledEquipment); }
                        coupledEquipment = manager.FindByCarGUID(thisEquipment.CarGuidCoupledRear);
                        if (coupledEquipment != null) { seekQ.Enqueue(coupledEquipment); }
                        while (seekQ.Count > 0 && carsForJob.Count < proceduralRuleset.maxCarsPerJob)
                        {
                            var possibleMatch = seekQ.Dequeue();
                            seenEquipment.Add(possibleMatch);

                            var possibleMatchLogicCar = possibleMatch.GetLogicCar();
                            if (!carsInYard.Contains(possibleMatchLogicCar) || !cargoGroup.cargoTypes.Contains(possibleMatchLogicCar.CurrentCargoTypeInCar)) { continue; }

                            carsForJob.Add(possibleMatchLogicCar);
                            coupledEquipment = manager.FindByCarGUID(possibleMatch.CarGuidCoupledFront);
                            if (coupledEquipment != null && !seenEquipment.Contains(coupledEquipment)) { seekQ.Enqueue(coupledEquipment); }
                            coupledEquipment = manager.FindByCarGUID(possibleMatch.CarGuidCoupledRear);
                            if (coupledEquipment != null && !seenEquipment.Contains(coupledEquipment)) { seekQ.Enqueue(coupledEquipment); }
                        }

                        // Generate the job, but only if it meets the minimum requirements
                        if (carsForJob.Count >= proceduralRuleset.minCarsPerJob)
                        {
                            jobChainController = procJobGenerator.GenerateHaulChainJobForCars(carsForJob.ToList());
                        }
                    }
                    else if (proceduralRuleset.unloadStartingJobSupported && thisEquipment.DestinationID == stationId && proceduralRuleset.inputCargoGroups.Any(group => group.cargoTypes.Contains(cargoTypeInCar)))
                    {
                        // Player previously hauled car here, generate shunting unload job
                    }
                }
                else
                {
                    if (proceduralRuleset.loadStartingJobSupported && licensedOutputCargoGroups.Any(group => group.cargoTypes.Any(cargoType => CargoTypes.CanCarContainCargoType(carType, cargoType))))
                    {
                        // Station can load cargo into this car & player is licensed to do so, generate shunting load job
                        var potentialCargoGroups = licensedOutputCargoGroups.Where(group => group.cargoTypes.Any(cargoType => CargoTypes.CanCarContainCargoType(carType, cargoType)));
                        var countCargoGroups = potentialCargoGroups.Count();
                        var indexInCargoGroups = rng.Next(countCargoGroups);
                        var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

                        DVOwnership.LogDebug(() => $"Attempting to generate shunting load job using cargo group {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

                        // Find all equipment that matches the selected cargo group
                        var potentialEmptyCars = carsInYard.Where(car => car.CurrentCargoTypeInCar == CargoType.None && cargoGroup.cargoTypes.Any(cargoType => CargoTypes.CanCarContainCargoType(car.carType, cargoType))).ToList();
                        var potentialEquipment = potentialEmptyCars.Select(car => manager.FindByCarGUID(car.carGuid)).ToList();
                        
                        // Group equipment into train sets
                        var contiguousEquipment = new List<HashSet<Equipment>>();
                        foreach (var currentEquipment in potentialEquipment)
                        {
                            var currentCarGUID = currentEquipment.CarGUID;
                            var contiguousSet = contiguousEquipment.Find(set => set.Any(equipmentFromSet => equipmentFromSet.IsCoupledTo(currentCarGUID)));

                            if (contiguousSet != null)
                            {
                                contiguousSet.Add(currentEquipment);
                                continue;
                            }

                            contiguousSet = new HashSet<Equipment>();
                            contiguousSet.Add(currentEquipment);
                            contiguousEquipment.Add(contiguousSet);
                        }

                        // Get the train set that includes the original car (and remove it from the list to avoid double processing)
                        var thisEquipmentSet = contiguousEquipment.Find(set => set.Contains(thisEquipment));
                        contiguousEquipment.Remove(thisEquipmentSet);

                        // Select train sets based on maximum requirements
                        var equipmentSetsForJob = new List<HashSet<Equipment>>();
                        equipmentSetsForJob.Add(thisEquipmentSet);
                        for (var index = 0; equipmentSetsForJob.Count < proceduralRuleset.maxShuntingStorageTracks && index < contiguousEquipment.Count; ++index)
                        {
                            var trainLengthSoFar = equipmentSetsForJob.Aggregate(0, (sum, set) => sum + set.Count);
                            var equipmentSet = contiguousEquipment[index];
                            if (trainLengthSoFar + equipmentSet.Count > proceduralRuleset.maxCarsPerJob) { continue; }

                            equipmentSetsForJob.Add(equipmentSet);
                        }
                        
                        // Add cars to carsForJob so that they'll be removed from carsInYard if a job is successfully generated
                        foreach (var equipmentSet in equipmentSetsForJob)
                        {
                            foreach (var equipment in equipmentSet)
                            {
                                var car = equipment.GetLogicCar();
                                carsForJob.Add(car);
                            }
                        }

                        // Generate the job, but only if it meets the minimum requirements
                        if (carsForJob.Count >= proceduralRuleset.minCarsPerJob)
                        {
                            var carSetsForJob =
                                from equipmentSet in equipmentSetsForJob
                                select (from equipment in equipmentSet select equipment.GetLogicCar()).ToList();
                            jobChainController = procJobGenerator.GenerateLoadChainJobForCars(carSetsForJob.ToList());
                        }
                    }
                }

                if (jobChainController != null)
                {
                    // TODO: what do we do with it?
                    carsInYard.ExceptWith(carsForJob);
                }
                else
                {
                    // Try again, but only after attempting to generate jobs for other cars first
                    carsQ.Enqueue(thisCar);
                    --attemptsRemaining;
                }
            }

            DVOwnership.Log(log.ToString());
            StationProceduralJobsController_Patches.ReportJobGenerationComplete(stationController);
            yield break;
        }

        private static List<Track> GetTracksByStationID (string stationId)
        {
            var allTracks = RailTrackRegistry.AllTracks;
            var stationTracks = from railTrack in allTracks where railTrack.logicTrack.ID.yardId == stationId select railTrack.logicTrack;
            return stationTracks.ToList();
        }
    }
}
