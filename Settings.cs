using UnityModManagerNet;

namespace DVOwnership
{
    internal class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Enable verbose logging")]
        public bool isLoggingEnabled =
#if DEBUG
            true;
#else
            false;
#endif

        public void OnChange() { }
    }
}
