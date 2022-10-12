using UnityModManagerNet;

namespace DVOwnership
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Enable verbose logging")]
        public bool isLoggingEnabled =
#if DEBUG
            true;
#else
            false;
#endif

        [Draw("Scale equipment price with career difficulty")]
        public bool isPriceScaledWithDifficulty = false;

        public void OnChange() { }
    }
}
