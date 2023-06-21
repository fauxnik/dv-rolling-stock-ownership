using DV;
using Harmony12;
using System.Collections.Generic;

namespace DVOwnership
{
	public class CommsRadio
	{
		public static CommsRadioController Controller => controller;
		private static CommsRadioController controller;

		[HarmonyPatch(typeof(CommsRadioController), "Awake")]
		class CommsRadioController_Awake_Patch
		{
			public static CommsRadioEquipmentPurchaser equipmentPurchaser = null;

			static void Postfix(CommsRadioController __instance, List<ICommsRadioMode> ___allModes)
			{
				controller = __instance;

				if (equipmentPurchaser == null) { equipmentPurchaser = controller.gameObject.AddComponent<CommsRadioEquipmentPurchaser>(); }

				if (!___allModes.Contains(equipmentPurchaser))
				{
					int spawnerIndex = ___allModes.FindIndex(mode => mode is CommsRadioCarSpawner);
					if (spawnerIndex != -1) { ___allModes.Insert(spawnerIndex, equipmentPurchaser); }
					else { ___allModes.Add(equipmentPurchaser); }
					controller.ReactivateModes();
				}
			}
		}
	}
}
