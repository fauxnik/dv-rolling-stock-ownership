using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DVOwnership;

internal static class LogsFolderOpener
{
	internal static bool TryOpenLogsFolder()
	{
		try
		{
			string logsPath = Path.Combine(new string[] { GetLocalLowPath(), "Altfuture", "Derail Valley" });
			if (!Directory.Exists(logsPath)) { return false; }

			string[] filesToSelect = { "Player.log" };
			OpenFolderAndSelectFiles(logsPath, filesToSelect);

			return true;
		}
		catch
		{
			return false;
		}
	}

	// based on https://stackoverflow.com/a/4495081/2085526
#region
	private static string GetLocalLowPath()
	{
		Guid localLowId = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
		return GetKnownFolderPath(localLowId);
	}

	private static string GetKnownFolderPath(Guid knownFolderId)
	{
		IntPtr pszPath = IntPtr.Zero;
		try
		{
			int hr = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pszPath);
			if (hr >= 0) { return Marshal.PtrToStringAuto(pszPath); }

			throw Marshal.GetExceptionForHR(hr);
		}
		finally
		{
			if (pszPath != IntPtr.Zero) { Marshal.FreeCoTaskMem(pszPath); }
		}
	}

	[DllImport("shell32.dll")]
	static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
#endregion

	// based on https://stackoverflow.com/a/3576444/2085526
#region
    [DllImport("shell32.dll", ExactSpelling = true)]
    public static extern int SHOpenFolderAndSelectItems(
        IntPtr pidlFolder,
        uint cidl,
        [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
        uint dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ILCreateFromPath([MarshalAs(UnmanagedType.LPTStr)] string pszPath);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    public interface IShellLinkW
    {
        [PreserveSig]
        int GetPath(StringBuilder pszFile, int cch, [In, Out] ref WIN32_FIND_DATAW pfd, uint fFlags);

        [PreserveSig]
        int GetIDList([Out] out IntPtr ppidl);

        [PreserveSig]
        int SetIDList([In] ref IntPtr pidl);

        [PreserveSig]
        int GetDescription(StringBuilder pszName, int cch);

        [PreserveSig]
        int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [PreserveSig]
        int GetWorkingDirectory(StringBuilder pszDir, int cch);

        [PreserveSig]
        int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        [PreserveSig]
        int GetArguments(StringBuilder pszArgs, int cch);

        [PreserveSig]
        int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        [PreserveSig]
        int GetHotkey([Out] out ushort pwHotkey);

        [PreserveSig]
        int SetHotkey(ushort wHotkey);

        [PreserveSig]
        int GetShowCmd([Out] out int piShowCmd);

        [PreserveSig]
        int SetShowCmd(int iShowCmd);

        [PreserveSig]
        int GetIconLocation(StringBuilder pszIconPath, int cch, [Out] out int piIcon);

        [PreserveSig]
        int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        [PreserveSig]
        int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        [PreserveSig]
        int Resolve(IntPtr hwnd, uint fFlags);

        [PreserveSig]
        int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode), BestFitMapping(false)]
    public struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    private static void OpenFolderAndSelectFiles(string folder, params string[] filesToSelect)
    {
        IntPtr dir = ILCreateFromPath(folder);

        var filesToSelectIntPtrs = new IntPtr[filesToSelect.Length];
        for (int i = 0; i < filesToSelect.Length; i++)
        {
            filesToSelectIntPtrs[i] = ILCreateFromPath(filesToSelect[i]);
        }

        SHOpenFolderAndSelectItems(dir, (uint) filesToSelect.Length, filesToSelectIntPtrs, 0);
        ReleaseComObject(dir);
        ReleaseComObject(filesToSelectIntPtrs);
    }

    private static void ReleaseComObject(params object[] comObjs)
    {
        foreach (object obj in comObjs)
        {
            if (obj != null && Marshal.IsComObject(obj))
                Marshal.ReleaseComObject(obj);
        }
    }
#endregion
}
