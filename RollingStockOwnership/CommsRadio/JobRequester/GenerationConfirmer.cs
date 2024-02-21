using CommsRadioAPI;
using DV;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RollingStockOwnership.CommsRadio.JobRequester;

internal class GenerationConfirmer : AStateBehaviour
{
	private static readonly StationController[] AllStations = GameObject.FindObjectsOfType<StationController>();

	private HashSet<StationController> NearbyStations;
	private bool Cancel;

	public GenerationConfirmer(HashSet<StationController>? nearbyStations = null, bool cancel = false) : base(
		new CommsRadioState(
			titleText: Main.Localize("comms_job_mode_title"),
			contentText: nearbyStations != null
				? Main.Localize("comms_job_confirmation_content", string.Join(", ", nearbyStations.Select(station => station.logicStation.ID)))
				: Main.Localize("comms_job_confirmation_error"),
			actionText: cancel || nearbyStations == null || nearbyStations.Count == 0
				? Main.Localize("comms_job_confirmation_action_negative")
				: Main.Localize("comms_job_confirmation_action_positive"),
			buttonBehaviour: ButtonBehaviourType.Override
		)
	) {
		NearbyStations = nearbyStations ?? new HashSet<StationController>();
		Cancel = cancel;
	}

	public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
	{
		switch (action)
		{
			case InputAction.Activate:
				if (!Cancel && NearbyStations != null)
				{
					Main.Log($"Attempting additional job generation at stations: {string.Join(", ", NearbyStations.Select(station => station.logicStation.ID))}");

					NearbyStations.Do(GenerateJobs);

					utility.PlaySound(VanillaSoundCommsRadio.Confirm);
				}
				else
				{
					utility.PlaySound(VanillaSoundCommsRadio.Cancel);
				}
				return new MainMenu();

			case InputAction.Up:
			case InputAction.Down:
				return new GenerationConfirmer(NearbyStations, !Cancel);

			default:
				throw new Exception($"Unexpected action: {action}");
		}
	}

	public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
	{
		bool isDirty = false;
		HashSet<StationController> stationsInRange = new HashSet<StationController>();

		foreach (StationController station in AllStations)
		{
			if (AccessTools.Field(typeof(StationController), "stationRange").GetValue(station) is StationJobGenerationRange stationRange)
			{
				if (stationRange.IsPlayerInJobGenerationZone(stationRange.PlayerSqrDistanceFromStationCenter))
				{
					stationsInRange.Add(station);
					if (!NearbyStations.Contains(station)) { isDirty = true; }
				}
			}
			else
			{
				Main.LogError($"Couldn't access private field \"stationRange\" of StationController with ID {station.logicStation.ID}");
			}
		}

		if (isDirty)
		{
			return new GenerationConfirmer(stationsInRange, Cancel);
		}

		return this;
	}

	private void GenerateJobs(StationController station)
	{
		ProceduralJobsController jobsController = ProceduralJobsController.ForStation(station);
		StationProceduralJobsController stationJobsController = station.ProceduralJobsController;
		var generationCoroField = AccessTools.Field(typeof(StationProceduralJobsController), "generationCoro");
		void onComplete() => generationCoroField.SetValue(stationJobsController, null);
		var generationCoro = stationJobsController.StartCoroutine(jobsController.GenerateJobsCoro(onComplete));
		generationCoroField.SetValue(stationJobsController, generationCoro);
	}
}
