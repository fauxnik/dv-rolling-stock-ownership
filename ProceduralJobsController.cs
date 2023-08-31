using DV;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.Utils;
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
			var licensedOutputCargoGroups = (from cargoGroup in proceduralRuleset.outputCargoGroups where LicenseManager_Patches.IsLicensedForCargoTypes(cargoGroup.cargoTypes) select cargoGroup).ToList();
			var manager = SingletonBehaviour<RollingStockManager>.Instance;

			DVObjectModel types = Globals.G.Types;

			lock (RollingStockManager.syncLock)
			{
				HashSet<Car> carsInYard;
				if (carsToUse != null)
				{
					carsInYard = carsToUse.ToHashSet();
				}
				else
				{
					// get all (logic) cars in the yard
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
							if (Utilities.IsAnySpecialCar(car.carType.v1)) { continue; }
							carsInYard.Add(car);
						}

						foreach (var car in track.GetCarsPartiallyOnTrack())
						{
							if (Utilities.IsAnySpecialCar(car.carType.v1)) { continue; }
							carsInYard.Add(car);
						}
					}

					// get all (logic) cars from player's train
					var playerTrainCars = PlayerManager.Car?.trainset?.cars ?? new List<TrainCar>();
					var playerCars = from trainCar in playerTrainCars where !trainCar.IsLoco select trainCar.logicCar;
					foreach (var car in playerCars) { carsInYard.Add(car); }
				}

				// get all (logic) cars with active jobs
				var carsWithJobs = new HashSet<Car>();
				JobsManager jobsManager = SingletonBehaviour<JobsManager>.Instance;
				var activeJobs = jobsManager.currentJobs;
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
						var nestedTasks = taskData.nestedTasks ?? new List<Task>();
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

				var minCarsPerJob = Math.Min(proceduralRuleset.minCarsPerJob, carsInYard.Count);
				var maxCarsPerJob = Math.Min(proceduralRuleset.maxCarsPerJob, SingletonBehaviour<LicenseManager>.Instance.GetMaxNumberOfCarsPerJobWithAcquiredJobLicenses());
				var maxShuntingStorageTracks = proceduralRuleset.maxShuntingStorageTracks;
				var haulStartingJobSupported = proceduralRuleset.haulStartingJobSupported;
				var unloadStartingJobSupported = proceduralRuleset.unloadStartingJobSupported;
				var loadStartingJobSupported = proceduralRuleset.loadStartingJobSupported;
				var inputCargoGroups = proceduralRuleset.inputCargoGroups;

				// loop, generating jobs for train cars, until all train cars are accounted for or we reach an upper bound of attempts
				var carsQ = new Queue<Car>();
				foreach (var car in carsInYard) { carsQ.Enqueue(car); }
				var attemptsRemaining = MAX_JOB_GENERATION_ATTEMPTS;
				var jobsGenerated = 0;
				while (attemptsRemaining > 0 && carsInYard.Count > 0 && carsQ.Count > 0)
				{
					yield return null;

					var thisCar = carsQ.Dequeue();
					if (!carsInYard.Contains(thisCar)) { continue; }

					DVOwnership.LogDebug(() => $"Attempting to generate job for car {thisCar.ID}.");

					JobChainController? jobChainController = null;
					if (!(manager.FindByCarGUID(thisCar.carGuid) is Equipment thisEquipment)) { continue; }
					var carsForJob = new HashSet<Car> { thisCar };

					string jobType = "unknown";
					var carType = thisCar.carType;
					var cargoTypeInCar = thisCar.CurrentCargoTypeInCar;
					if (cargoTypeInCar != CargoType.None)
					{
						if (haulStartingJobSupported && licensedOutputCargoGroups.Any(group => group.cargoTypes.Contains(cargoTypeInCar)))
						{
							// Player previously loaded car here, generate freight haul job
							jobType = JobType.Transport.ToString();
							var potentialCargoGroups = licensedOutputCargoGroups.Where(group => group.cargoTypes.Contains(cargoTypeInCar));
							var countCargoGroups = potentialCargoGroups.Count();
							var indexInCargoGroups = rng.Next(countCargoGroups);
							var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

							DVOwnership.LogDebug(() => $"Attempting to generate freight haul job using cargo group {cargoGroup}, {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

							yield return null;
							carsForJob.UnionWith(GetMatchingCoupledCars(thisEquipment, cargoGroup, carsInYard, maxCarsPerJob - carsForJob.Count));

							// Generate the job, but only if it meets the length requirements
							if (carsForJob.Count >= minCarsPerJob && carsForJob.Count <= maxCarsPerJob)
							{
								DVOwnership.LogDebug(() => $"Generating freight haul job for {carsForJob.Count} cars: [{string.Join(", ", carsForJob.Select(car => car.ID))}]");
								yield return null;
								jobChainController = ProceduralJobGenerators.GenerateHaulChainJobForCars(rng, carsForJob.ToList(), cargoGroup, stationController);
								DVOwnership.LogDebug(() => "Generation OK");
							}
							else if (carsForJob.Count > maxCarsPerJob)
							{
								DVOwnership.LogDebug(() => $"Exceeded the maximum number of cars per job ({maxCarsPerJob}).");
							}
							else
							{
								DVOwnership.LogDebug(() => $"Didn't meet the minimum number of cars per job ({minCarsPerJob}).");
							}
						}
						else if (unloadStartingJobSupported && thisEquipment?.DestinationID == stationId && inputCargoGroups.Any(group => group.cargoTypes.Contains(cargoTypeInCar)))
						{
							// Player previously hauled car here, generate shunting unload job
							jobType = JobType.ShuntingUnload.ToString();
							var potentialCargoGroups = inputCargoGroups.Where(group => group.cargoTypes.Contains(cargoTypeInCar));
							var countCargoGroups = potentialCargoGroups.Count();
							var indexInCargoGroups = rng.Next(countCargoGroups);
							var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

							DVOwnership.LogDebug(() => $"Attempting to generate shunting unload job using cargo group {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

							yield return null;
							carsForJob.UnionWith(GetMatchingCoupledCars(thisEquipment, cargoGroup, carsInYard, maxCarsPerJob - carsForJob.Count));

							// Generate the job, but only if it meets the length requirements
							if (carsForJob.Count >= minCarsPerJob && carsForJob.Count <= maxCarsPerJob)
							{
								DVOwnership.LogDebug(() => $"Generating shunting unload job for {carsForJob.Count} cars: [{string.Join(", ", carsForJob.Select(car => car.ID))}]");
								yield return null;
								jobChainController = ProceduralJobGenerators.GenerateUnloadChainJobForCars(rng, carsForJob.ToList(), cargoGroup, stationController);
								DVOwnership.LogDebug(() => "Generation OK");
							}
							else if (carsForJob.Count > maxCarsPerJob)
							{
								DVOwnership.LogDebug(() => $"Exceeded the maximum number of cars per job ({maxCarsPerJob}).");
							}
							else
							{
								DVOwnership.LogDebug(() => $"Didn't meet the minimum number of cars per job ({minCarsPerJob}).");
							}
						}
					}
					else
					{
						if (loadStartingJobSupported && licensedOutputCargoGroups.Any(group => group.cargoTypes.Any(cargoType => types.CargoType_to_v2[cargoType].IsLoadableOnCarType(carType.parentType))))
						{
							// Station can load cargo into this car & player is licensed to do so, generate shunting load job
							jobType = JobType.ShuntingLoad.ToString();
							var potentialCargoGroups = licensedOutputCargoGroups.Where(group => group.cargoTypes.Any(cargoType => types.CargoType_to_v2[cargoType].IsLoadableOnCarType(carType.parentType)));
							var countCargoGroups = potentialCargoGroups.Count();
							var indexInCargoGroups = rng.Next(countCargoGroups);
							var cargoGroup = potentialCargoGroups.ElementAt(indexInCargoGroups);

							DVOwnership.LogDebug(() => $"Attempting to generate shunting load job using cargo group {indexInCargoGroups + 1} of {countCargoGroups} possible groups.");

							yield return null;

							// Find all equipment that matches the selected cargo group
							var potentialEmptyCars = carsInYard.Where(car => car.CurrentCargoTypeInCar == CargoType.None && cargoGroup.cargoTypes.Any(cargoType => types.CargoType_to_v2[cargoType].IsLoadableOnCarType(carType.parentType))).ToList();
							var potentialEquipment = potentialEmptyCars.Select(car => manager.FindByCarGUID(car.carGuid)).ToList();

							yield return null;

							// Group equipment into train sets
							var contiguousEquipment = new List<HashSet<Equipment>>();
							foreach (var currentEquipment in potentialEquipment)
							{
								if (currentEquipment == null) { continue; }

								var currentCarGUID = currentEquipment.CarGUID;
								var contiguousSet = contiguousEquipment.Find(set => set.Any(equipmentFromSet => equipmentFromSet.IsCoupledTo(currentCarGUID)));

								if (contiguousSet == null)
								{
									contiguousSet = new HashSet<Equipment>();
									contiguousEquipment.Add(contiguousSet);
								}

								contiguousSet.Add(currentEquipment);
							}

							LogContiguousEquipment(contiguousEquipment);

							yield return null;

							// Get the train set that includes the original car (and remove it from the list to avoid double processing)
							var thisEquipmentSet = thisEquipment != null ? contiguousEquipment.Find(set => set.Contains(thisEquipment)) : new HashSet<Equipment>();
							contiguousEquipment.Remove(thisEquipmentSet);

							yield return null;

							// Select train sets based on maximum requirements
							var equipmentSetsForJob = new List<HashSet<Equipment>> { thisEquipmentSet };
							for (var index = 0; equipmentSetsForJob.Count < maxShuntingStorageTracks && index < contiguousEquipment.Count; ++index)
							{
								var trainLengthSoFar = equipmentSetsForJob.Aggregate(0, (sum, set) => sum + set.Count);
								var equipmentSet = contiguousEquipment[index];
								if (trainLengthSoFar + equipmentSet.Count > maxCarsPerJob) { continue; }

								equipmentSetsForJob.Add(equipmentSet);
							}

							yield return null;

							// Add cars to carsForJob so that they'll be removed from carsInYard if a job is successfully generated
							foreach (var equipmentSet in equipmentSetsForJob)
							{
								foreach (var equipment in equipmentSet)
								{
									if (equipment.GetLogicCar() is Car car) { carsForJob.Add(car); }
									else { DVOwnership.LogError($"Logic car for equipment with ID {equipment.ID} not found. This shouldn't happen."); }
								}
							}

							// Generate the job, but only if it meets the length requirements
							if (carsForJob.Count >= minCarsPerJob && carsForJob.Count <= maxCarsPerJob)
							{
								DVOwnership.LogDebug(() => $"Generating shunting load job for {carsForJob.Count} cars: [{string.Join(", ", carsForJob.Select(car => car.ID))}]");
								yield return null;
								var carSetsForJob =
									from equipmentSet in equipmentSetsForJob
									select (from equipment in equipmentSet select equipment.GetLogicCar()).ToList();
								jobChainController = ProceduralJobGenerators.GenerateLoadChainJobForCars(rng, carSetsForJob.ToList(), cargoGroup, stationController);
								DVOwnership.LogDebug(() => "Generation OK");
							}
							else if (carsForJob.Count > maxCarsPerJob)
							{
								DVOwnership.LogDebug(() => $"Exceeded the maximum number of cars per job ({maxCarsPerJob}).");
							}
							else
							{
								DVOwnership.LogDebug(() => $"Didn't meet the minimum number of cars per job ({minCarsPerJob}).");
							}
						}
					}

					if (jobChainController != null)
					{
						// TODO: what do we do with it?
						jobsGenerated++;
						var trainCarsForJob = jobChainController.trainCarsForJobChain;
						carsInYard.ExceptWith(from trainCar in trainCarsForJob select trainCar.logicCar);
						log.Append($"Generated {jobType} job with cars {string.Join(", ", trainCarsForJob.Select(tc => tc.ID))}.\n");
					}
					else
					{
						// Try again, but only after attempting to generate jobs for other cars first
						carsQ.Enqueue(thisCar);
						--attemptsRemaining;
					}
				}

				yield return null;

				var attemptsUnsuccessful = MAX_JOB_GENERATION_ATTEMPTS - attemptsRemaining;
				log.Append($"Generated a total of {jobsGenerated} jobs with {attemptsUnsuccessful}/{MAX_JOB_GENERATION_ATTEMPTS} unsuccessful attempts.");
			}

			DVOwnership.Log(log.ToString());
			if (onComplete != null)
			{
				onComplete();
			}
			yield break;
		}

		private static HashSet<Car> GetMatchingCoupledCars(Equipment equipment, CargoGroup cargoGroup, HashSet<Car> carsInYard, int maxCarsToReturn)
		{
			var manager = SingletonBehaviour<RollingStockManager>.Instance;
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

		private static IEnumerable<Track> GetTracksByStationID (string stationId)
		{
			var allTracks = RailTrackRegistry.Instance.AllTracks;
			return from railTrack in allTracks where railTrack.logicTrack.ID.yardId == stationId select railTrack.logicTrack;
		}

		private static void LogContiguousEquipment(List<HashSet<Equipment>> contiguousEquipment)
		{
			static string joinEquipmentIDsFromHashSet(HashSet<Equipment> set) => string.Join(", ", set.Select(eq => eq.ID));
			var contiguousEquipmentIDs = string.Join(", ", contiguousEquipment.Select(set => $"[{joinEquipmentIDsFromHashSet(set)}]"));
			DVOwnership.LogDebug(() => $"Contiguous Equipment: [{contiguousEquipmentIDs}]");
		}
	}
}
