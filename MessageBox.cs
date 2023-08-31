using DV;
using DV.UI;
using DV.UIFramework;
using DV.Utils;
using System.Collections;

namespace DVOwnership
{
	public static class MessageBox
	{
		public static void ShowPopupOk(string message, string title = "", PopupClosedDelegate? onClose = null)
		{
			ShowPopup(uiReferences.popupOk, new PopupLocalizationKeys { titleKey = title, labelKey = message }, onClose);
		}

		public static void ShowPopupYesNo(string message, string title = "", PopupClosedDelegate? onClose = null)
		{
			ShowPopup(uiReferences.popupYesNo, new PopupLocalizationKeys { titleKey = title, labelKey = message }, onClose);
		}

		public static void ShowPopup3Buttons(string message, string title = "", string positive = "Yes", string negative = "No", string abort = "Abort", PopupClosedDelegate? onClose = null)
		{
			ShowPopup(uiReferences.popup3Buttons, new PopupLocalizationKeys {
				titleKey = title,
				labelKey = message,
				positiveKey = positive,
				negativeKey = negative,
				abortionKey = abort,
			}, onClose);
		}

		private static void ShowPopup(Popup prefab, PopupLocalizationKeys locKeys, PopupClosedDelegate? onClose)
		{
			if (WorldStreamingInit.IsLoaded)
			{
				SingletonBehaviour<CoroutineManager>.Instance.Run(Coro(prefab, locKeys, onClose));
			}
			else
			{
				WorldStreamingInit.LoadingFinished += () => SingletonBehaviour<CoroutineManager>.Instance.Run(Coro(prefab, locKeys, onClose));
			}
		}

		private static IEnumerator Coro(Popup prefab, PopupLocalizationKeys locKeys, PopupClosedDelegate? onClose)
		{
			while (SingletonBehaviour<AppUtil>.Instance.IsTimePaused)
				yield return null;
			while (!PopupManager.CanShowPopup())
				yield return null;
			Popup popup = PopupManager.ShowPopup(prefab, locKeys, keepLiteralData: true);
			if (onClose != null) { popup.Closed += onClose; }
		}

		private static PopupManager PopupManager
		{
			get => SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.PopupManager;
		}

		private static PopupNotificationReferences uiReferences
		{
			get => SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.uiReferences;
		}
	}
}
