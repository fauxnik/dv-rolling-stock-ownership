using HarmonyLib;

namespace DVOwnership.Patches
{
    public class PlayerManager_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up player manager patches, but they've already been set up!");
                return;
            }

            DVOwnership.Log("Setting up PlayerManager patches.");

            isSetup = true;
            var PlayerManager_SetCar = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.SetCar));
            var PlayerManager_SetCar_Prefix = AccessTools.Method(typeof(PlayerManager_Patches), nameof(SetCar_Prefix));
            var PlayerManager_SetCar_Postfix = AccessTools.Method(typeof(PlayerManager_Patches), nameof(SetCar_Postfix));
            DVOwnership.Patch(PlayerManager_SetCar, prefix: new HarmonyMethod(PlayerManager_SetCar_Prefix), postfix: new HarmonyMethod(PlayerManager_SetCar_Postfix));
        }

        static void SetCar_Prefix(out TrainCar __state)
        {
            __state = PlayerManager.LastLoco;
        }

        static void SetCar_Postfix(TrainCar __state, ref TrainCar ___LastLoco)
        {
            var prevLoco = __state;
            var nextLoco = PlayerManager.LastLoco;
            var isNextLocoStationary = nextLoco == null || nextLoco.isStationary;
            var isPrevLocoMoving = prevLoco != null && !prevLoco.isStationary;
            if (nextLoco != prevLoco && isNextLocoStationary && isPrevLocoMoving)
            {
                DVOwnership.Log($"Restoring {prevLoco} as Player's last loco, because it is moving and {nextLoco} is not.");
                ___LastLoco = prevLoco;
            }
        }
    }
}
