using DV;
using HarmonyLib;
using UnityEngine;

namespace RollingStockOwnership.Extensions;

internal static class CarDestinationHighlighter_Extensions
{
	public static void TurnOn(this CarDestinationHighlighter carDestinationHighlighter)
	{
		if (AccessTools.Field(typeof(CarDestinationHighlighter), "highlighterGO").GetValue(carDestinationHighlighter) is GameObject highlighterGO)
		{
			highlighterGO.SetActive(true);
		}

		if (AccessTools.Field(typeof(CarDestinationHighlighter), "directionArrowGO").GetValue(carDestinationHighlighter) is GameObject directionArrowGO)
		{
			directionArrowGO.SetActive(true);
		}
	}
}
