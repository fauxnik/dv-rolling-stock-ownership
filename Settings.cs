using UnityModManagerNet;

namespace DVOwnership
{
	public class Settings : UnityModManager.ModSettings, IDrawable
	{
		[Draw("Log level")]
		public LogLevel selectedLogLevel =
#if DEBUG
			LogLevel.Info;
#else
			LogLevel.Warn;
#endif

		[Draw("Scale equipment price with career difficulty")]
		public bool isPriceScaledWithDifficulty = false;

		public void OnChange() { }
	}
}
