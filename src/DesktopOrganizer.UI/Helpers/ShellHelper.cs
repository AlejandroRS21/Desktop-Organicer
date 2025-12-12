using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace DesktopOrganizer.UI.Helpers;

public static class ShellHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string lpVerb;
        public string lpFile;
        public string lpParameters;
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private const int SW_SHOW = 5;
    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

    public static void ShowProperties(string filename)
    {
        var info = new SHELLEXECUTEINFO();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = filename;
        info.nShow = SW_SHOW;
        info.fMask = SEE_MASK_INVOKEIDLIST;
        ShellExecuteEx(ref info);
    }

    public static void OpenWith(string filename)
    {
        var args = Path.Combine(Environment.SystemDirectory, "shell32.dll");
        // "OpenAs_RunDLL" entry point expects "hwnd,hinst,cmdline,ncmdshow" if called via rundll32
        // Correct syntax: rundll32.exe shell32.dll,OpenAs_RunDLL C:\Path\To\File
        Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {filename}");
    }

    public static void OpenInExplorer(string filename)
    {
         Process.Start("explorer.exe", $"/select,\"{filename}\"");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;

    public static void DeleteToRecycleBin(string filename)
    {
        var shf = new SHFILEOPSTRUCT();
        shf.wFunc = FO_DELETE;
        shf.pFrom = filename + '\0'; // Multiple files must be null-separated and double-null terminated
        shf.fFlags = FOF_ALLOWUNDO; 
        // Allow undo = Recycle Bin.
        // We can let Windows show confirmation dialog (default) or suppress it.
        // User logic has confirmation dialog in ViewModel. So we can suppress Windows one if desired, or relying on Windows one?
        // ViewModel already shows "Are you sure?".
        // So we might suppress windows confirmation. 
        // But double confirmation is annoying.
        
        SHFileOperation(ref shf);
    }
}
