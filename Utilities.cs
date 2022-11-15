using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVOwnership
{
    public static class Utilities
    {
        public static T GetRandomFromList<T>(System.Random rng, List<T> list)
        {
            return list[rng.Next(list.Count)];
        }

        public static List<CarsPerTrack> GetRandomSortingOfCarsOnTracks(System.Random rng, List<Track> tracks, List<Car> allCarsForJobChain, int maxNumberOfStorageTracks)
        {
            if (tracks == null || tracks.Count == 0) { return null; }

            int numCars = allCarsForJobChain.Count;
            int numTracks = Mathf.Min(new int[]
            {
                rng.Next(1, maxNumberOfStorageTracks + 1),
                tracks.Count,
                numCars
            });
            int averageNumCarsPerTrack = Mathf.FloorToInt((float)numCars / (float)numTracks);

            List<int> numCarsPerTracks = new List<int>();
            int numCarsAccountedFor = 0;
            for (int i = 0; i < numTracks; ++i)
            {
                int numCarsForCurrentTrack;
                if (i == numTracks - 1)
                {
                    numCarsForCurrentTrack = numCars - numCarsAccountedFor;
                }
                else
                {
                    numCarsForCurrentTrack = rng.Next(1, averageNumCarsPerTrack + 1);
                }
                if (numCarsForCurrentTrack < 1)
                {
                    DVOwnership.LogError("Assigned zero cars to a track. This should never happen!");
                }
                numCarsPerTracks.Add(numCarsForCurrentTrack);
                numCarsAccountedFor += numCarsForCurrentTrack;
            }
            numCarsPerTracks.Sort((a, b) => b - a); // reverse sort

            YardTracksOrganizer yto = SingletonBehaviour<YardTracksOrganizer>.Instance;
            List<CarsPerTrack> carsPerTracks = new List<CarsPerTrack>();
            tracks = new List<Track>(tracks); // cloning is required to prevent modifying the external list

            for (int index = 0, cursor = 0; index < numCarsPerTracks.Count; cursor += numCarsPerTracks[index++])
            {
                int numCarsForCurrentTrack = numCarsPerTracks[index];
                List<Car> carsForCurrentTrack = allCarsForJobChain.GetRange(cursor, numCarsForCurrentTrack);

                float approximateLengthOfCarsForCurrentTrack = yto.GetTotalCarsLength(carsForCurrentTrack) + yto.GetSeparationLengthBetweenCars(carsForCurrentTrack.Count);

                List<Track> tracksWithRequiredFreeSpace = yto.FilterOutTracksWithoutRequiredFreeSpace(tracks, approximateLengthOfCarsForCurrentTrack);
                if (tracksWithRequiredFreeSpace.Count < 1)
                {
                    DVOwnership.LogWarning($"Couldn't find a track with enough free space. ({approximateLengthOfCarsForCurrentTrack})");
                    return null;
                }

                Track track = Utilities.GetRandomFromList(rng, tracksWithRequiredFreeSpace);
                tracks.Remove(track);
                carsPerTracks.Add(new CarsPerTrack(track, carsForCurrentTrack));
            }

            return carsPerTracks;
        }

        public static PaymentCalculationData ExtractPaymentCalculationData(List<Car> cars)
        {
            return ExtractPaymentCalculationData(cars, cars.Select(car => car.CurrentCargoTypeInCar).ToList());
        }
        public static PaymentCalculationData ExtractPaymentCalculationData(List<Car> cars, List<CargoType> cargoTypes)
        {
            return ExtractPaymentCalculationData(cars.Select(car => car.carType).ToList(), cargoTypes);
        }
        public static PaymentCalculationData ExtractPaymentCalculationData(List<TrainCarType> carTypes, List<CargoType> cargoTypes)
        {
            Dictionary<TrainCarType, int> countEachCarType = new Dictionary<TrainCarType, int>();
            Dictionary<CargoType, int> countEachCargoType = new Dictionary<CargoType, int>();

            foreach (var carType in carTypes)
            {
                if (!countEachCarType.ContainsKey(carType)) { countEachCarType.Add(carType, 0); }
                countEachCarType[carType]++;
            }

            foreach (var cargoType in cargoTypes)
            {
                if (!countEachCargoType.ContainsKey(cargoType)) { countEachCargoType.Add(cargoType, 0); }
                countEachCargoType[cargoType]++;
            }

            return new PaymentCalculationData(countEachCarType, countEachCargoType);
        }

        public static List<CarsPerCargoType> ExtractCarsPerCargoType(List<Car> cars)
        {
            var cargoTypeToCars = new Dictionary<CargoType, List<Car>>();

            foreach (var car in cars)
            {
                var cargoType = car.CurrentCargoTypeInCar;
                if (!cargoTypeToCars.ContainsKey(cargoType))
                {
                    cargoTypeToCars.Add(cargoType, new List<Car>());
                }
                cargoTypeToCars[cargoType].Add(car);
            }

            var carsPerCargoTypes = from kv in cargoTypeToCars
                                    let cargoType = kv.Key
                                    let carsForCargoType = kv.Value
                                    let cargoAmount = (from car in carsForCargoType select car.LoadedCargoAmount).Sum()
                                    select new CarsPerCargoType(cargoType, carsForCargoType, cargoAmount);

            return carsPerCargoTypes.ToList();
        }
    }
}
