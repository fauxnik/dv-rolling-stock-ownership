using UnityModManagerNet;

namespace RollingStockOwnership;

public class Settings : UnityModManager.ModSettings, IDrawable
{
	[Draw("Log level")]
	public LogLevel logLevel_v2 = // versioning let's us reset players' selected log level
#if DEBUG
		LogLevel.Debug;
#else
		LogLevel.Info;
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
