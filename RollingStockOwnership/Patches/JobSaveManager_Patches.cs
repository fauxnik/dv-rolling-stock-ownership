﻿using DV;
using DV.Logic.Job;
using HarmonyLib;
using System.Linq;

namespace RollingStockOwnership.Patches;

public class JobSaveManager_Patches
{
	private static bool isSetup = false;

	public static void Setup()
	{
		if (isSetup)
		{
			Main.LogWarning("Trying to set up JobSaveManager patches, but they've already been set up!");
			return;
		}

		Main.Log("Setting up JobSaveManager patches.");

		isSetup = true;
		var JobSaveManager_GetYardTrackWithId = AccessTools.Method(typeof(JobSaveManager), "GetYardTrackWithId");
		var JobSaveManager_GetYardTrackWithId_Postfix = AccessTools.Method(typeof(JobSaveManager_Patches), nameof(GetYardTrackWithId_Postfix));
		Main.Patch(JobSaveManager_GetYardTrackWithId, postfix: new HarmonyMethod(JobSaveManager_GetYardTrackWithId_Postfix));
	}

	static void GetYardTrackWithId_Postfix(string trackId, ref Track __result)
	{
		__result ??= WorldData.Instance.OrderedRailtracks.Select(railTrack => railTrack.logicTrack).FirstOrDefault(logicTrack => logicTrack.ID.FullID == trackId);
	}
}
