using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopOrganizer.UI.Helpers;

public static class DesktopWindowHelper
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    static extern bool SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOACTIVATE = 0x0010;

    public static bool SetToDesktop(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var workerW = GetWorkerW();
        
        if (workerW != IntPtr.Zero)
        {
            IntPtr result = SetParent(handle, workerW);
            return result != IntPtr.Zero;
        }
        
        // Fallback to Progman
        IntPtr progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            IntPtr result = SetParent(handle, progman);
            return result != IntPtr.Zero;
        }

        return false;
    }

    private static IntPtr GetWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr result = IntPtr.Zero;
        
        // Spawn WorkerW
        SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0x0000, 1000, out result);

        IntPtr workerW = IntPtr.Zero;

        EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
        {
            IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (p != IntPtr.Zero)
            {
                // Found the window with SHELLDLL_DefView. 
                // The WorkerW we want is the NEXT sibling in the Z-order.
                workerW = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
            }

            return true;
        }), IntPtr.Zero);

        return workerW;
    }

    public static void SendToBottom(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
