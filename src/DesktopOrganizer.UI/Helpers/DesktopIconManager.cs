using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace DesktopOrganizer.UI.Helpers;

/// <summary>
/// Manages hiding/showing desktop icons by moving them off-screen
/// This creates the illusion that icons in Fences are "removed" from the desktop
/// </summary>
public class DesktopIconManager
{
    #region Windows API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, ref POINT lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll")]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesRead);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint LVM_FIRST = 0x1000;
    private const uint LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const uint LVM_GETITEMTEXT = LVM_FIRST + 115; // LVM_GETITEMTEXTW
    private const uint LVM_SETITEMPOSITION = LVM_FIRST + 15;
    private const uint LVM_GETITEMPOSITION = LVM_FIRST + 16;
    private const uint LVM_ARRANGE = LVM_FIRST + 22;
    private const uint LVIF_TEXT = 0x0001;

    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LVITEM
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    #endregion

    private IntPtr _desktopListView = IntPtr.Zero;
    
    // Store original positions so we can restore them
    private Dictionary<string, (int X, int Y)> _originalPositions = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _hiddenIcons = new(StringComparer.OrdinalIgnoreCase);
    
    // Off-screen position (way outside visible area)
    private const int OFF_SCREEN_X = -10000;
    private const int OFF_SCREEN_Y = -10000;

    public DesktopIconManager()
    {
        FindDesktopListView();
    }

    private void FindDesktopListView()
    {
        // Find the desktop window hierarchy:
        // Progman -> SHELLDLL_DefView -> SysListView32
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return;

        IntPtr shellDefView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellDefView == IntPtr.Zero)
        {
            // Try WorkerW windows (for Windows 10/11 with slideshow wallpaper)
            IntPtr workerW = IntPtr.Zero;
            do
            {
                workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                if (workerW != IntPtr.Zero)
                {
                    shellDefView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellDefView != IntPtr.Zero)
                        break;
                }
            } while (workerW != IntPtr.Zero);
        }

        if (shellDefView == IntPtr.Zero)
            return;

        _desktopListView = FindWindowEx(shellDefView, IntPtr.Zero, "SysListView32", null);
    }

    /// <summary>
    /// Hide icons by moving them off-screen. Stores original positions for restoration.
    /// </summary>
    public void HideIcons(IEnumerable<string> fileNames)
    {
        if (_desktopListView == IntPtr.Zero)
            FindDesktopListView();

        if (_desktopListView == IntPtr.Zero)
            return;

        var fileNamesToHide = fileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            int itemCount = (int)SendMessage(_desktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            
            GetWindowThreadProcessId(_desktopListView, out uint processId);
            
            IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
            if (hProcess == IntPtr.Zero)
                return;

            try
            {
                for (int i = 0; i < itemCount; i++)
                {
                    string? itemText = GetItemText(hProcess, i);
                    if (string.IsNullOrEmpty(itemText))
                        continue;

                    // Check if this icon should be hidden
                    if (fileNamesToHide.Contains(itemText) && !_hiddenIcons.Contains(itemText))
                    {
                        // Save original position
                        var currentPos = GetItemPosition(i);
                        if (currentPos.HasValue && currentPos.Value.X > -5000) // Not already hidden
                        {
                            _originalPositions[itemText] = currentPos.Value;
                        }

                        // Move off-screen
                        SetItemPosition(i, OFF_SCREEN_X, OFF_SCREEN_Y);
                        _hiddenIcons.Add(itemText);
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error hiding icons: {ex.Message}");
        }
    }

    /// <summary>
    /// Show previously hidden icons by restoring their original positions.
    /// </summary>
    public void ShowIcons(IEnumerable<string> fileNames)
    {
        if (_desktopListView == IntPtr.Zero)
            return;

        var fileNamesToShow = fileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            int itemCount = (int)SendMessage(_desktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            
            GetWindowThreadProcessId(_desktopListView, out uint processId);
            
            IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
            if (hProcess == IntPtr.Zero)
                return;

            try
            {
                for (int i = 0; i < itemCount; i++)
                {
                    string? itemText = GetItemText(hProcess, i);
                    if (string.IsNullOrEmpty(itemText))
                        continue;

                    if (fileNamesToShow.Contains(itemText) && _hiddenIcons.Contains(itemText))
                    {
                        // Restore original position
                        if (_originalPositions.TryGetValue(itemText, out var originalPos))
                        {
                            SetItemPosition(i, originalPos.X, originalPos.Y);
                        }
                        else
                        {
                            // If no original position saved, let Windows auto-arrange
                            SetItemPosition(i, 100 + (i * 80), 100);
                        }
                        
                        _hiddenIcons.Remove(itemText);
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing icons: {ex.Message}");
        }
    }

    /// <summary>
    /// Show all hidden icons and restore their positions.
    /// </summary>
    public void ShowAllIcons()
    {
        if (_desktopListView == IntPtr.Zero || _hiddenIcons.Count == 0)
            return;

        ShowIcons(_hiddenIcons.ToList());
        _hiddenIcons.Clear();
        _originalPositions.Clear();
    }

    /// <summary>
    /// Get the current position of a desktop icon by index.
    /// </summary>
    private (int X, int Y)? GetItemPosition(int itemIndex)
    {
        POINT pt = new POINT();
        SendMessage(_desktopListView, LVM_GETITEMPOSITION, itemIndex, ref pt);
        return (pt.X, pt.Y);
    }

    /// <summary>
    /// Set the position of a desktop icon by index.
    /// </summary>
    private void SetItemPosition(int itemIndex, int x, int y)
    {
        // Pack coordinates into lParam: MAKELPARAM(x, y)
        IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
        SendMessage(_desktopListView, LVM_SETITEMPOSITION, new IntPtr(itemIndex), lParam);
    }

    private string? GetItemText(IntPtr hProcess, int itemIndex)
    {
        const int bufferSize = 260;
        IntPtr pBuffer = VirtualAllocEx(hProcess, IntPtr.Zero, bufferSize * 2, MEM_COMMIT, PAGE_READWRITE);
        if (pBuffer == IntPtr.Zero)
            return null;

        try
        {
            LVITEM item = new LVITEM
            {
                mask = LVIF_TEXT,
                iItem = itemIndex,
                iSubItem = 0,
                pszText = pBuffer,
                cchTextMax = bufferSize
            };

            int itemSize = Marshal.SizeOf(typeof(LVITEM));
            IntPtr pItem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)itemSize, MEM_COMMIT, PAGE_READWRITE);
            if (pItem == IntPtr.Zero)
                return null;

            try
            {
                byte[] itemBytes = new byte[itemSize];
                IntPtr localPtr = Marshal.AllocHGlobal(itemSize);
                try
                {
                    Marshal.StructureToPtr(item, localPtr, false);
                    Marshal.Copy(localPtr, itemBytes, 0, itemSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(localPtr);
                }

                if (!WriteProcessMemory(hProcess, pItem, itemBytes, (uint)itemSize, out _))
                    return null;

                SendMessage(_desktopListView, LVM_GETITEMTEXT, new IntPtr(itemIndex), pItem);

                byte[] textBytes = new byte[bufferSize * 2];
                if (!ReadProcessMemory(hProcess, pBuffer, textBytes, (uint)(bufferSize * 2), out _))
                    return null;

                return Encoding.Unicode.GetString(textBytes).TrimEnd('\0');
            }
            finally
            {
                VirtualFreeEx(hProcess, pItem, 0, MEM_RELEASE);
            }
        }
        finally
        {
            VirtualFreeEx(hProcess, pBuffer, 0, MEM_RELEASE);
        }
    }

    /// <summary>
    /// Get list of all desktop icon names.
    /// </summary>
    public List<string> GetAllDesktopIconNames()
    {
        var result = new List<string>();
        
        if (_desktopListView == IntPtr.Zero)
            FindDesktopListView();

        if (_desktopListView == IntPtr.Zero)
            return result;

        try
        {
            int itemCount = (int)SendMessage(_desktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            
            GetWindowThreadProcessId(_desktopListView, out uint processId);
            
            IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
            if (hProcess == IntPtr.Zero)
                return result;

            try
            {
                for (int i = 0; i < itemCount; i++)
                {
                    string? itemText = GetItemText(hProcess, i);
                    if (!string.IsNullOrEmpty(itemText))
                    {
                        result.Add(itemText);
                    }
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting icon names: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Refresh the desktop view.
    /// </summary>
    public void RefreshDesktop()
    {
        // Notify shell of changes
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        
        if (_desktopListView != IntPtr.Zero)
        {
            // Force a refresh via F5 simulation
            const uint WM_KEYDOWN = 0x0100;
            const uint WM_KEYUP = 0x0101;
            const int VK_F5 = 0x74;
            
            SendMessage(_desktopListView, WM_KEYDOWN, new IntPtr(VK_F5), IntPtr.Zero);
            SendMessage(_desktopListView, WM_KEYUP, new IntPtr(VK_F5), IntPtr.Zero);
        }
    }

    /// <summary>
    /// Check if an icon is currently hidden.
    /// </summary>
    public bool IsIconHidden(string fileName)
    {
        return _hiddenIcons.Contains(fileName);
    }

    /// <summary>
    /// Get count of hidden icons.
    /// </summary>
    public int HiddenIconCount => _hiddenIcons.Count;
}
