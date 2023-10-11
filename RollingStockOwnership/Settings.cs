using UnityModManagerNet;

namespace RollingStockOwnership;

public class Settings : UnityModManager.ModSettings, IDrawable
{
	[Draw("Log level")]
	public LogLevel selectedLogLevel =
#if DEBUG
		LogLevel.Debug;
#else
		LogLevel.Warn;
#endif

	[Draw("Sandbox price multiplier", Min = 0f, Max = 1f)]
	public float sandboxPriceMultiplier = 0f;

	[Draw("Starter locomotive")]
	public StarterLocoType starterLocoType = StarterLocoType.LocoDE2;

	public void OnChange() { }

	public override void Save(UnityModManager.ModEntry modEntry)
	{
		Save<Settings>(this, modEntry);
	}
}
