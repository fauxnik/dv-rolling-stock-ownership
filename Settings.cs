using UnityModManagerNet;
using UnityEngine;
using System;

namespace DVOwnership
{
	public class Settings : UnityModManager.ModSettings, IDrawable
	{
		public LogLevel selectedLogLevel =
#if DEBUG
			LogLevel.Debug;
#else
			LogLevel.Warn;
#endif
		public float sandboxPriceMultiplier = 0f;
		public StarterLocoType starterLocoType = StarterLocoType.LocoDE2;

		public void OnChange() { }

		public override void Save(UnityModManager.ModEntry modEntry)
		{
			Save<Settings>(this, modEntry);
		}

		private bool choosingLogLevel = false;
		private bool choosingStarterLoco = false;

		public void CustomDraw(UnityModManager.ModEntry _)
		{
			GUI.skin.label.stretchWidth = false;
			GUI.skin.button.stretchWidth = false;
			GUI.skin.button.fixedHeight = GUI.skin.button.lineHeight * 1.45f;

			GUILayout.BeginVertical();

			RenderEnumOption<LogLevel>("Log level", ref selectedLogLevel, ref choosingLogLevel);

			GUILayout.BeginHorizontal();
			GUILayout.Label("Sandbox price multiplier");
			GUILayout.Space(5);
			string multiplierString = GUILayout.TextField(sandboxPriceMultiplier.ToString());
			if (float.TryParse(multiplierString, out float multiplierFloat))
			{
				sandboxPriceMultiplier = Math.Abs(multiplierFloat);
			}
			GUILayout.EndHorizontal();

			RenderEnumOption<StarterLocoType>("Starter locomotive", ref starterLocoType, ref choosingStarterLoco);

			GUILayout.EndVertical();
		}

		private static void RenderEnumOption<T>(string label, ref T value, ref bool expanded) where T : Enum
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);
			GUILayout.Space(5);
			if (!expanded)
			{
				if (GUILayout.Button(value.ToString()))
				{
					expanded = true;
				}
			}
			else
			{
				foreach (T item in Enum.GetValues(typeof(T)))
				{
					string formatting = item.Equals(value) ? "<b><color=lightblue>{0}</color></b>" : "{0}";
					if (GUILayout.Button(string.Format(formatting, item)))
					{
						value = item;
						expanded = false;
					}
				}
			}
			GUILayout.EndHorizontal();
		}
	}
}
