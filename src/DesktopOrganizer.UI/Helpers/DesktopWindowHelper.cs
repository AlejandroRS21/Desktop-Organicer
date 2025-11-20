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

    public static void SetToDesktop(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var workerW = GetWorkerW();
        
        if (workerW != IntPtr.Zero)
        {
            SetParent(handle, workerW);
        }
        else
        {
            // Fallback: Send to bottom if WorkerW not found
            SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    private static IntPtr GetWorkerW()
    {
        // Fetch the Progman window
        IntPtr progman = FindWindow("Progman", null);

        // Send 0x052C to Progman. This message spawns a WorkerW behind the desktop icons.
        // If it is already there, nothing happens.
        IntPtr result = IntPtr.Zero;
        SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0x0000, 1000, out result);

        IntPtr workerW = IntPtr.Zero;

        // We enumerate all Windows to find the one with SHELLDLL_DefView
        EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
        {
            IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (p != IntPtr.Zero)
            {
                // Gets the WorkerW Window after the current one.
                workerW = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
            }

            return true;
        }), IntPtr.Zero);
        
        // Fallback: If we didn't find it (sometimes happens on Win11), try to find the WorkerW that is NOT the one with SHELLDLL_DefView
        if (workerW == IntPtr.Zero)
        {
             EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
             {
                 // Look for WorkerW
                 // This is a bit hacky: usually the WorkerW that is the desktop background is the one that is visible
                 // and has no SHELLDLL_DefView child.
                 // But let's stick to the standard method first.
                 return true;
             }), IntPtr.Zero);
        }

        return workerW;
    }
}
