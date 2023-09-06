using DV.UIFramework;
using UnityEngine;

namespace DVOwnership;

internal static class CriticalFailureMessage
{
	internal static void ShowAndQuit()
	{
		#if DEBUG
		string message = "";
#else
		string message = "Rolling Stock Ownership has been disabled due to an unrecoverable failure.\n\n";
#endif
		message += "Please search the mod's Github issue tracker for a relevant report. If none is found, please open one and include the log file from this session.\n\n";
		message += "Would you like me to open the logs folder for you?";

		MessageBox.ShowPopupYesNo(title: "[RSO] Critical Error", message: message, onClose: (result) => {
			string message = "";

			if (result.closedBy == PopupClosedByAction.Positive)
			{
				bool isFolderOpened = LogsFolderOpener.TryOpenLogsFolder();
				if (!isFolderOpened)
				{
					message += "Couldn't open the logs folder.\n\n";
				}
			}

			message += "The game will now close.";

			MessageBox.ShowPopupOk(title: "[RSO] Critical Error", message: message, onClose: (_) => { Application.Quit(); });
		});
	}
}
