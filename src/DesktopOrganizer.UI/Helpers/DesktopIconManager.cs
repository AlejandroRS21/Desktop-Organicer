using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Windows;

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

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct LVHITTESTINFO
    {
        public POINT pt;
        public uint flags;
        public int iItem;
        public int iSubItem;
        public int iGroup;
    }

    private const uint LVM_HITTEST = LVM_FIRST + 18;
    private const uint LVHT_NOWHERE = 0x0001;
    private const uint LVHT_ONITEMICON = 0x0002;
    private const uint LVHT_ONITEMLABEL = 0x0004;

    /// <summary>
    /// Checks if the given screen point is over an icon or label.
    /// </summary>
    public bool IsOverIcon(int screenX, int screenY)
    {
        if (_desktopListView == IntPtr.Zero)
            FindDesktopListView();
            
        if (_desktopListView == IntPtr.Zero) return false;

        // Convert Screen to Client
        POINT pt = new POINT { X = screenX, Y = screenY };
        ScreenToClient(_desktopListView, ref pt);

        // Prepare HitTest info in remote process memory is NOT needed for LVM_HITTEST?
        // Wait, LVM_HITTEST usually requires the struct to be in the process memory if it's cross-process?
        // Actually, for common controls, some messages work cross-process, but LVM_HITTEST involves a pointer.
        // So yes, we need to inject memory.
        
        uint processId;
        GetWindowThreadProcessId(_desktopListView, out processId);
        IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
        if (hProcess == IntPtr.Zero) return false;

        IntPtr pInfo = IntPtr.Zero;
        try
        {
            LVHITTESTINFO info = new LVHITTESTINFO();
            info.pt = pt;
            
            pInfo = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)Marshal.SizeOf(typeof(LVHITTESTINFO)), MEM_COMMIT, PAGE_READWRITE);
            
            // Write struct to target
            byte[] data = new byte[Marshal.SizeOf(typeof(LVHITTESTINFO))];
            IntPtr temp = Marshal.AllocHGlobal(data.Length);
            Marshal.StructureToPtr(info, temp, false);
            Marshal.Copy(temp, data, 0, data.Length);
            Marshal.FreeHGlobal(temp);
            
            uint written;
            WriteProcessMemory(hProcess, pInfo, data, (uint)data.Length, out written);

            // Send Message
            int result = (int)SendMessage(_desktopListView, LVM_HITTEST, IntPtr.Zero, pInfo);
            
            // Logic: result is index, but we might want to check flags too.
            // But usually result != -1 means we hit something.
            return result != -1;
        }
        finally
        {
            if (pInfo != IntPtr.Zero) VirtualFreeEx(hProcess, pInfo, 0, MEM_RELEASE);
            CloseHandle(hProcess);
        }
    }

    [DllImport("user32.dll")]
    static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    #endregion

    private IntPtr _desktopListView = IntPtr.Zero;
    
    // Store original positions so we can restore them
    private Dictionary<string, (int X, int Y)> _originalPositions = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _hiddenIcons = new(StringComparer.OrdinalIgnoreCase);
    
    // Off-screen position (way outside visible area)
    private const int OFF_SCREEN_X = -10000;
    private const int OFF_SCREEN_Y = -10000;

    private void WriteLog(string msg)
    {
        try {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "desktop_hide_debug.txt");
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff}: {msg}\n");
        } catch {}
    }

    public DesktopIconManager()
    {
        FindDesktopListView();
    }

    private void FindDesktopListView()
    {
        // Find the desktop window hierarchy:
        // Progman -> SHELLDLL_DefView -> SysListView32
        // OR
        // WorkerW -> SHELLDLL_DefView -> SysListView32
        
        IntPtr shellDefView = IntPtr.Zero;
        IntPtr progman = FindWindow("Progman", null);
        
        if (progman != IntPtr.Zero)
        {
            shellDefView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        }

        if (shellDefView == IntPtr.Zero)
        {
            // Try WorkerW windows
            EnumWindows((hwnd, lParam) =>
            {
                IntPtr defView = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero)
                {
                    shellDefView = defView;
                    return false; // Stop enumerating
                }
                return true;
            }, IntPtr.Zero);
        }

        if (shellDefView != IntPtr.Zero)
        {
            _desktopListView = FindWindowEx(shellDefView, IntPtr.Zero, "SysListView32", null);
            WriteLog($"Found Desktop ListView: {_desktopListView:X}");
        }
        else 
        {
            WriteLog("FAILED to find Desktop ListView");
        }
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
    /// Hides the entire desktop icon window (ListView).
    /// </summary>
    public void HideDesktopListView()
    {
        if (_desktopListView == IntPtr.Zero) FindDesktopListView();
        if (_desktopListView != IntPtr.Zero)
        {
            WriteLog("Attempting to HIDE Desktop ListView");
            ShowWindow(_desktopListView, SW_HIDE);
            
            // Fallback: Also move all icons just in case window hiding is bypassed by refresh
            HideAllIcons();
        }
    }

    /// <summary>
    /// Shows the desktop icon window (ListView).
    /// </summary>
    public void ShowDesktopListView()
    {
        if (_desktopListView == IntPtr.Zero) FindDesktopListView();
        if (_desktopListView != IntPtr.Zero)
        {
            ShowWindow(_desktopListView, SW_SHOW);
        }
    }

    /// <summary>
    /// Hide ALL desktop icons by moving them off-screen.
    /// </summary>
    public void HideAllIcons()
    {
        if (_desktopListView == IntPtr.Zero)
            FindDesktopListView();

        if (_desktopListView == IntPtr.Zero)
            return;

        try
        {
            int itemCount = (int)SendMessage(_desktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            GetWindowThreadProcessId(_desktopListView, out uint processId);
            IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
            
            if (hProcess == IntPtr.Zero) return;

            try
            {
                for (int i = 0; i < itemCount; i++)
                {
                    string? itemText = GetItemText(hProcess, i);
                    if (string.IsNullOrEmpty(itemText)) continue;

                    if (!_hiddenIcons.Contains(itemText))
                    {
                        var currentPos = GetItemPosition(i);
                        if (currentPos.HasValue && currentPos.Value.X > -5000)
                        {
                            _originalPositions[itemText] = currentPos.Value;
                        }

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
            System.Diagnostics.Debug.WriteLine($"Error hiding all icons: {ex.Message}");
        }
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
    public void SetItemPosition(int itemIndex, int x, int y)
    {
        // Pack coordinates into lParam: MAKELPARAM(x, y)
        IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
        SendMessage(_desktopListView, LVM_SETITEMPOSITION, new IntPtr(itemIndex), lParam);
    }

    /// <summary>
    /// Identify icons located within a specific screen rectangle.
    /// </summary>
    public List<string> GetIconsInRect(Rect bounds)
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
            
            if (hProcess == IntPtr.Zero) return result;

            try 
            {
                for (int i = 0; i < itemCount; i++)
                {
                    var pos = GetItemPosition(i);
                    if (pos.HasValue)
                    {
                        // Check if center of icon or top-left is effectively inside
                        // Let's assume icon size is roughly 70x70, we check if the point is inside
                        if (bounds.Contains(new Point(pos.Value.X, pos.Value.Y)))
                        {
                             string? name = GetItemText(hProcess, i);
                             if (!string.IsNullOrEmpty(name))
                             {
                                 result.Add(name);
                             }
                        }
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
            System.Diagnostics.Debug.WriteLine($"Error finding icons in rect: {ex.Message}");
        }

        return result;
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
