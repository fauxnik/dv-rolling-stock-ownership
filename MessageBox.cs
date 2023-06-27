#nullable enable
using Harmony12;
using System;
using System.Collections;

// Copyright 2020 Miles Spielberg

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject
// to the following conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
// ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

namespace DVOwnership
{
	public static class MessageBox
	{
		public static void ShowMessage(string message, bool pauseGame)
		{
			if (WorldStreamingInit.IsLoaded)
			{
				StartCoro(message, pauseGame);
			}
			else
			{
				WorldStreamingInit.LoadingFinished += () => StartCoro(message, pauseGame);
			}
		}

		private static void StartCoro(string message, bool pauseGame)
		{
			SingletonBehaviour<CanvasSpawner>.Instance.StartCoroutine(Coro(message, pauseGame));
		}

		private static IEnumerator Coro(string message, bool pauseGame)
		{
			while (!SingletonBehaviour<CanvasSpawner>.Instance.MenuLoaded)
				yield return null;
			while (DV.AppUtil.IsPaused)
				yield return null;
			while (SingletonBehaviour<CanvasSpawner>.Instance.IsOpen)
				yield return null;
			yield return WaitFor.Seconds(1f);
			MenuScreen? menuScreen = SingletonBehaviour<CanvasSpawner>.Instance.CanvasGO.transform.Find("TutorialPrompt")?.GetComponent<MenuScreen>();
			TutorialPrompt? tutorialPrompt = menuScreen?.GetComponentInChildren<TutorialPrompt>(includeInactive: true);
			if (menuScreen != null && tutorialPrompt != null)
			{
				tutorialPrompt.SetText(message);
				SingletonBehaviour<CanvasSpawner>.Instance.Open(menuScreen, pauseGame);
			}
		}

		public static void OnClosed(Action action)
		{
			var canvasSpawner = SingletonBehaviour<CanvasSpawner>.Instance;
			var screenSwitcher = AccessTools.Field(typeof(CanvasSpawner), "screenSwitcher").GetValue(canvasSpawner) as MenuScreenSwitcher;
			if (screenSwitcher != null) { screenSwitcher.MenuClosed += action; }
		}
	}
}
