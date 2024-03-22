using DV.Localization;
using DV.ThingTypes;
using DV.Utils;
using RollingStockOwnership.Patches;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager.ModSettings;
using DVLangHelper.Runtime;

namespace RollingStockOwnership;

public static class Main
{
#nullable disable // these are set or created when the mod loads
	private static UnityModManager.ModEntry modEntry;
	private static Harmony harmony;

	public static Settings Settings { get; private set; }
#nullable restore
	public static Version Version { get { return modEntry.Version; } }
	public static string DisplayName { get { return modEntry.Info.DisplayName; } }
	public static string Id { get { return modEntry.Info.Id; } }

	static void OnLoad(UnityModManager.ModEntry loadedEntry)
	{
		modEntry = loadedEntry;

		if (!modEntry.Enabled) { return; }

		// TODO: figure out if HasUpdate actually works; Intellisense displays "Not used"
		//if (modEntry.HasUpdate)
		//{
		//	NeedsUpdate();
		//	return;
		//}

		try { Settings = Load<Settings>(modEntry); }
		catch {
			LogWarning("Unabled to load mod settings. Using defaults instead.");
			Settings = new Settings();
		}
		modEntry.OnGUI = Settings.Draw;
		modEntry.OnSaveGUI = Settings.Save;

		try
		{
			harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			var translations = new TranslationInjector("fauxnik/dv-rolling-stock-ownership");
			translations.AddTranslationsFromCsv(Path.Combine(modEntry.Path, "offline_translations.csv"));
			translations.AddTranslationsFromWebCsv("https://docs.google.com/spreadsheets/d/e/2PACX-1vQGeGpv-zk-TxxN3c87vjhtwJdP2oOYeJHF5nI2cJshF7mrNGHTeqQFOda0fo-zOltSRfNdT_nrHNiW/pub?gid=1191351766&single=true&output=csv");
		}
		catch (Exception e) { OnCriticalFailure(e, "patching miscellaneous assembly"); }

		try
		{
			CarSpawner.Instance.CarSpawned += (wagon) => {
				void CargoLoaded(CargoType _)
				{
					ReservationManager.Instance.Reserve(wagon);
				}

				wagon.CargoLoaded -= CargoLoaded;
				wagon.CargoLoaded += CargoLoaded;

				void CargoUnloaded()
				{
					ReservationManager.Instance.Release(wagon);
				}

				wagon.CargoUnloaded -= CargoUnloaded;
				wagon.CargoUnloaded += CargoUnloaded;
			};
		}
		catch (Exception e) { OnCriticalFailure(e, "setting up reservation callbacks"); }

		try { CargoTypes_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching CargoTypes"); }

		try { CommsRadioCarDeleter_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching CommsRadioCarDeleter"); }

		try { IdGenerator_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching IdGenerator"); }

		try { JobChainControllerWithEmptyHaulGeneration_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching JobChainControllerWithEmptyHaulGeneration"); }

		try { JobSaveManager_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching JobSaveManager"); }

		try { CommsRadioController_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching Preferences"); }

		try { StationLocoSpawner_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching StationLocoSpawner"); }

		try { StationProceduralJobsController_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching StationProceduralJobsController"); }

		try { Track_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching Track"); }

		try { TrainCar_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching TrainCar"); }

		try { UnusedTrainCarDeleter_Patches.Setup(); }
		catch (Exception e) { OnCriticalFailure(e, "patching UnusedTrainCarDeleter"); }

		CommsRadioAPI.ControllerAPI.Ready += CommsRadio.EquipmentPurchaserMode.Create;
#if DEBUG
		CommsRadioAPI.ControllerAPI.Ready += CommsRadio.JobRequesterMode.Create;
#endif

		WorldStreamingInit.LoadingFinished += StartingConditions.Verify;
	}

	internal static string Localize(string nakedKey, params string[] paramValues) =>
		LocalizationAPI.L(NamespaceKey(nakedKey), paramValues);

	private static string NamespaceKey(string nakedKey) =>
		$"rolling_stock_ownership/{nakedKey.Replace(" ", "_").ToLowerInvariant()}";

	public static MethodInfo Patch(
		MethodBase original,
		HarmonyMethod? prefix = null,
		HarmonyMethod? postfix = null,
		HarmonyMethod? transpiler = null
	) => harmony.Patch(original, prefix, postfix, transpiler);

	public static void LogVerbose(System.Func<object> messageFactory) =>
		LogAtLevel(messageFactory, LogLevel.Verbose);

	public static void LogDebug(System.Func<object> messageFactory) =>
		LogAtLevel(messageFactory, LogLevel.Debug);

	private static void LogAtLevel(System.Func<object> messageFactory, LogLevel level)
	{
		if (Settings.selectedLogLevel > level) { return; }

		var message = messageFactory();
		if (message is string) { modEntry.Logger.Log(message as string); }
		else
		{
			modEntry.Logger.Log("Logging object via UnityEngine.Debug...");
			Debug.Log(message);
		}
	}

	public static void Log(object message)
	{
		if (Settings.selectedLogLevel > LogLevel.Info) { return; }

		if (message is string) { modEntry.Logger.Log(message as string); }
		else
		{
			modEntry.Logger.Log("Logging object via UnityEngine.Debug...");
			Debug.Log(message);
		}
	}

	public static void LogWarning(object message)
	{
		if (Settings.selectedLogLevel > LogLevel.Warn) { return; }

		modEntry.Logger.Warning($"{message}");
	}

	public static void LogError(object message)
	{
		if (Settings.selectedLogLevel > LogLevel.Error) { return; }

		modEntry.Logger.Error($"{message}");
	}

	public static void OnCriticalFailure(Exception exception, string action)
	{
		// TODO: show floaty message (and offer to open log folder?) before quitting game
		Debug.Log(exception);
#if DEBUG
#else
		modEntry.Enabled = false;
		modEntry.Logger.Critical("Deactivating mod DVOwnership due to unrecoverable failure!");
#endif
		modEntry.Logger.Critical($"This happened while {action}.");
		modEntry.Logger.Critical($"You can reactivate DVOwnership by restarting the game, but this failure type likely indicates an incompatibility between the mod and a recent game update. Please search the mod's Github issue tracker for a relevant report. If none is found, please open one and include this log file.");
		Application.Quit();
	}

	private static void NeedsUpdate()
	{
		// TODO: show floaty message before quitting game
		modEntry.Logger.Critical($"There is a new version of DVOwnership available. Please install it to continue using the mod.\nInstalled: {modEntry.Version}\nLatest: {modEntry.NewestVersion}");
		Application.Quit();
	}
}
