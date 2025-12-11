using System;
using System.Runtime.InteropServices;

namespace DesktopOrganizer.UI.Helpers;

/// <summary>
/// Provides reliable methods to toggle desktop icon visibility using Windows Shell commands.
/// This method is more reliable than trying to manipulate individual icons.
/// </summary>
public static class DesktopIconToggler
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private const uint WM_COMMAND = 0x0111;
    private const int TOGGLE_DESKTOP_ICONS_COMMAND = 0x7402; // Shell command to toggle desktop icons

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private static bool _iconsHidden = false;

    /// <summary>
    /// Toggle the visibility of all desktop icons.
    /// </summary>
    public static void ToggleDesktopIcons()
    {
        IntPtr hShellView = GetShellViewWindow();
        if (hShellView != IntPtr.Zero)
        {
            // Send the toggle command to the shell view
            SendMessage(hShellView, WM_COMMAND, new IntPtr(TOGGLE_DESKTOP_ICONS_COMMAND), IntPtr.Zero);
            _iconsHidden = !_iconsHidden;
        }
        else
        {
            // Fallback: Hide/show the SysListView32 directly
            IntPtr hListView = GetDesktopListView();
            if (hListView != IntPtr.Zero)
            {
                if (_iconsHidden)
                {
                    ShowWindow(hListView, SW_SHOW);
                    _iconsHidden = false;
                }
                else
                {
                    ShowWindow(hListView, SW_HIDE);
                    _iconsHidden = true;
                }
            }
        }
    }

    /// <summary>
    /// Hide all desktop icons.
    /// </summary>
    public static void HideDesktopIcons()
    {
        if (!_iconsHidden)
        {
            ToggleDesktopIcons();
        }
    }

    /// <summary>
    /// Show all desktop icons.
    /// </summary>
    public static void ShowDesktopIcons()
    {
        if (_iconsHidden)
        {
            ToggleDesktopIcons();
        }
    }

    /// <summary>
    /// Check if desktop icons are currently visible.
    /// </summary>
    public static bool AreIconsVisible()
    {
        IntPtr hListView = GetDesktopListView();
        if (hListView != IntPtr.Zero)
        {
            return IsWindowVisible(hListView);
        }
        return !_iconsHidden;
    }

    private static IntPtr GetShellViewWindow()
    {
        // Find Progman
        IntPtr hProgman = FindWindow("Progman", null);
        if (hProgman == IntPtr.Zero)
            return IntPtr.Zero;

        // Find SHELLDLL_DefView under Progman
        IntPtr hShellView = FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
        
        if (hShellView == IntPtr.Zero)
        {
            // Try finding under WorkerW (Windows 10/11 with slideshow wallpaper)
            IntPtr hDesktop = FindWindow(null, null);
            IntPtr hWorkerW = IntPtr.Zero;
            
            do
            {
                hWorkerW = FindWindowEx(IntPtr.Zero, hWorkerW, "WorkerW", null);
                if (hWorkerW != IntPtr.Zero)
                {
                    hShellView = FindWindowEx(hWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (hShellView != IntPtr.Zero)
                        break;
                }
            } while (hWorkerW != IntPtr.Zero);
        }

        return hShellView;
    }

    private static IntPtr GetDesktopListView()
    {
        IntPtr hShellView = GetShellViewWindow();
        if (hShellView != IntPtr.Zero)
        {
            return FindWindowEx(hShellView, IntPtr.Zero, "SysListView32", null);
        }
        return IntPtr.Zero;
    }
}
