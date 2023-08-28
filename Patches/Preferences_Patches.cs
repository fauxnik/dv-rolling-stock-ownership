using HarmonyLib;

namespace DVOwnership.Patches
{
    public static class Preferences_Patches
    {
        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVOwnership.LogWarning("Trying to set up preferences patches, but they've already been set up!");
                return;
            }

            DVOwnership.Log("Setting up Preferences patches.");

            isSetup = true;
            /*var PreferenceUtils_IsExcluded = AccessTools.Method(typeof(PreferencesUtils), "IsExcluded");
            var PreferenceUtils_IsExcluded_Postfix = AccessTools.Method(typeof(Preferences_Patches), "IsExcluded_Postfix");
            DVOwnership.Patch(PreferenceUtils_IsExcluded, postfix: new HarmonyMethod(PreferenceUtils_IsExcluded_Postfix));*/
        }

        /*static void IsExcluded_Postfix(ref bool __result, Preferences p)
        {
            __result = true;
        }*/
    }
}
