# Guía Completa: Implementación de una Aplicación como Fences en .NET 10

## Índice
1. [Introducción y Arquitectura](#introducción-y-arquitectura)
2. [Componentes del Sistema](#componentes-del-sistema)
3. [Interacción con la Shell de Windows](#interacción-con-la-shell-de-windows)
4. [Ocultación de Iconos del Escritorio](#ocultación-de-iconos-del-escritorio)
5. [Gestión de Fences (Rejas)](#gestión-de-fences-rejas)
6. [Persistencia de Datos](#persistencia-de-datos)
7. [Implementación en .NET 10](#implementación-en-net-10)
8. [Referencias y Ejemplos](#referencias-y-ejemplos)

---

## 1. Introducción y Arquitectura

### 1.1 ¿Qué es una Aplicación Tipo Fences?

Una aplicación tipo Fences es un organizador de escritorio que permite:
- **Crear áreas virtuales** (fences) en el escritorio
- **Agrupar iconos** en contenedores visuales translúcidos
- **Ocultar/mostrar iconos** de forma selectiva
- **Guardar y restaurar** configuraciones del escritorio
- **Integrar portales de carpetas** que reflejan carpetas del sistema

### 1.2 Arquitectura General

```
┌─────────────────────────────────────────────────────────┐
│         Aplicación .NET 10 (Interfaz Principal)         │
├─────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────┐   │
│  │  WPF/WinUI 3 - Interfaz de Usuario               │   │
│  │  (Gestión de Fences, Configuración, UI)         │   │
│  └──────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────┐   │
│  │  P/Invoke Layer (Interop con Win32)              │   │
│  │  - User32.dll                                    │   │
│  │  - Shell32.dll                                   │   │
│  │  - CommCtrl.dll (ListView API)                   │   │
│  └──────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────┐   │
│  │  Sistema de Persistencia                         │   │
│  │  - Registry (HKCU)                               │   │
│  │  - Archivos de Configuración (JSON/XML)          │   │
│  │  - Snapshots del Escritorio                      │   │
│  └──────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│          Windows Desktop Shell Integration               │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Progman          (Program Manager)              │   │
│  │  ├─ SHELLDLL_DefView  (Shell View Container)     │   │
│  │  │  └─ SysListView32   (ListView de Iconos)      │   │
│  │  └─ WorkerW        (Fondo de escritorio)         │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Componentes del Sistema

### 2.1 Jerarquía de Ventanas del Escritorio

#### En Windows Estándar:
```
Progman (Program Manager)
└── SHELLDLL_DefView
    └── SysListView32 (FolderView) ← Aquí están los iconos
```

#### En Windows con Tema de Rotación de Fondo:
```
Progman (Program Manager)
└── WorkerW (uno de varios)
    └── SHELLDLL_DefView
        └── SysListView32 (FolderView) ← Aquí están los iconos
```

### 2.2 Estructura de Datos de un Icon en ListView

Cada icono en el escritorio es un elemento de ListView con:

```csharp
public struct LVITEM
{
    public int mask;              // Especifica qué miembros están válidos
    public int iItem;             // Índice del elemento
    public int iSubItem;          // Subíndice (siempre 0 para iconos)
    public uint state;            // Estado del elemento
    public uint stateMask;        // Máscara de estado
    public IntPtr pszText;        // Puntero al texto
    public int cchTextMax;        // Longitud máxima del texto
    public int iImage;            // Índice de la imagen
    public IntPtr lParam;         // Parámetro de aplicación
    public int iIndent;           // Indentación
    public int iGroupId;          // ID del grupo
    public uint cColumns;         // Número de columnas
    public IntPtr puColumns;      // Puntero a columnas
}
```

### 2.3 Estructura de Datos de Posición

Para manipular posiciones de iconos:

```csharp
public struct POINT
{
    public int X;
    public int Y;
}

// Convertido a IntPtr para envíos de mensajes:
// IntPtr lParam = (IntPtr)((Y << 16) | (X & 0xFFFF));
```

---

## 3. Interacción con la Shell de Windows

### 3.1 P/Invoke Declarations Necesarias

```csharp
using System.Runtime.InteropServices;

public static class Win32API
{
    // Constantes de Mensajes
    public const uint WM_COMMAND = 0x111;
    public const uint LVM_SETITEMPOSITION = 4111;  // 0x100F
    public const uint LVM_GETITEMPOSITION = 4112;  // 0x1010
    public const uint LVM_DELETEITEM = 4104;       // 0x1008
    public const uint LVM_GETITEM = 4101;          // 0x1005
    public const uint LVM_SETITEM = 4102;          // 0x1006
    public const uint LVM_GETITEMCOUNT = 4100;     // 0x1004
    public const uint LVM_GETITEMTEXT = 4145;      // 0x1039
    public const uint LVM_FINDITEM = 4179;         // 0x1053
    public const uint LVM_ARRANGE = 4118;          // 0x1016

    // Flags para ListView
    public const int LVIF_TEXT = 0x0001;
    public const int LVIF_IMAGE = 0x0002;
    public const int LVIF_PARAM = 0x0004;
    public const int LVIF_STATE = 0x0008;
    public const int LVIF_INDENT = 0x0010;
    public const int LVIF_GROUPID = 0x0100;

    public const uint LVIS_SELECTED = 0x0002;
    public const uint LVIS_FOCUSED = 0x0001;
    public const uint LVIS_HIDDEN = 0x0004;

    // Constantes de GetWindow
    public const uint GW_CHILD = 5;
    public const uint GW_HWNDNEXT = 2;
    public const uint GW_HWNDPREV = 3;

    // Constantes de Registro
    public const int TOGGLE_DESKTOP_COMMAND = 0x7402;

    // P/Invoke Functions
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, 
                                             string lpszClass, string lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowInfo(IntPtr hwnd, ref WINDOWINFO pwi);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, 
                                             ref POINT lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("shell32.dll")]
    public static extern int SHGetSetSettings(ref SHELLSTATE lpss, uint dwMask, bool bSet);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint RegisterWindowMessage(string lpString);

    // Estructuras
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWINFO
    {
        public uint cbSize;
        public RECT rcWindow;
        public RECT rcClient;
        public uint dwStyle;
        public uint dwExStyle;
        public uint dwWindowStatus;
        public uint cxWindowBorders;
        public uint cyWindowBorders;
        public ushort atomWindowType;
        public ushort wCreatorVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHELLSTATE
    {
        public uint dwMask;
        public bool fShowAllObjects;
        public bool fShowExtensions;
        public bool fNoConfirmRecycle;
        public bool fPrintObjects;
        public bool fMapNetDrvBtn;
        public bool fShowStatusBar;
        public bool fDoubleClickInWebView;
        public bool fDesktopNameSpace;
        public bool fWin95Classic;
        public bool fDontPrettyPath;
        public bool fShowCompColor;
        public bool fShowDesc;
        public bool fHideIcons;                    // ← IMPORTANTE: Oculta/muestra iconos
        public bool fWebView;
        public bool fFilter;
        public bool fShowSuperHidden;
        public bool fNoNetCrawling;
    }

    // Constantes para SHELLSTATE
    public const uint SSF_SHOWALLOBJECTS = 0x0001;
    public const uint SSF_SHOWEXTENSIONS = 0x0002;
    public const uint SSF_HIDEICONS = 0x4000;      // ← Bandera para ocultación de iconos
    public const uint SSF_SHOWCOMPCOLOR = 0x0008;
    public const uint SSF_SHOWSYSFILES = 0x0020;
    public const uint SSF_DOUBLECLICKINNAMEAREA = 0x0080;
}

[StructLayout(LayoutKind.Sequential)]
public struct LVITEM
{
    public int mask;
    public int iItem;
    public int iSubItem;
    public uint state;
    public uint stateMask;
    public IntPtr pszText;
    public int cchTextMax;
    public int iImage;
    public IntPtr lParam;
    public int iIndent;
    public int iGroupId;
    public uint cColumns;
    public IntPtr puColumns;
}
```

### 3.2 Localizar el ListView del Escritorio

Esta es una tarea crítica porque la estructura puede variar según el tema de Windows:

```csharp
public class DesktopWindow
{
    private IntPtr _desktopListViewHandle = IntPtr.Zero;

    /// <summary>
    /// Encuentra el handle del ListView del escritorio
    /// Maneja tanto la estructura Progman → SHELLDLL_DefView → SysListView32
    /// como Progman → WorkerW → SHELLDLL_DefView → SysListView32
    /// </summary>
    public IntPtr GetDesktopListViewHandle()
    {
        if (_desktopListViewHandle != IntPtr.Zero && Win32API.IsWindow(_desktopListViewHandle))
            return _desktopListViewHandle;

        IntPtr hShellViewWin = IntPtr.Zero;
        IntPtr hWorkerW = IntPtr.Zero;
        
        // Obtener Progman
        IntPtr hProgman = Win32API.FindWindow("Progman", "Program Manager");
        if (hProgman == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr hDesktopWnd = Win32API.GetDesktopWindow();

        // Intentar encontrar SHELLDLL_DefView directamente bajo Progman
        hShellViewWin = Win32API.FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
        
        // Si no lo encontramos, buscar en WorkerW windows (para temas con rotación de fondo)
        if (hShellViewWin == IntPtr.Zero)
        {
            hWorkerW = IntPtr.Zero;
            do
            {
                hWorkerW = Win32API.FindWindowEx(hDesktopWnd, hWorkerW, "WorkerW", null);
                if (hWorkerW != IntPtr.Zero)
                {
                    hShellViewWin = Win32API.FindWindowEx(hWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                }
            } while (hShellViewWin == IntPtr.Zero && hWorkerW != IntPtr.Zero);
        }

        // Ahora encontrar SysListView32 bajo SHELLDLL_DefView
        if (hShellViewWin != IntPtr.Zero)
        {
            _desktopListViewHandle = Win32API.FindWindowEx(hShellViewWin, IntPtr.Zero, 
                                                            "SysListView32", "FolderView");
        }

        return _desktopListViewHandle;
    }

    /// <summary>
    /// Obtener el handle de SHELLDLL_DefView directamente
    /// </summary>
    public IntPtr GetShellDefViewHandle()
    {
        IntPtr hProgman = Win32API.FindWindow("Progman", "Program Manager");
        if (hProgman == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr hShellViewWin = Win32API.FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
        
        if (hShellViewWin == IntPtr.Zero)
        {
            IntPtr hDesktopWnd = Win32API.GetDesktopWindow();
            IntPtr hWorkerW = IntPtr.Zero;
            
            do
            {
                hWorkerW = Win32API.FindWindowEx(hDesktopWnd, hWorkerW, "WorkerW", null);
                if (hWorkerW != IntPtr.Zero)
                {
                    hShellViewWin = Win32API.FindWindowEx(hWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                }
            } while (hShellViewWin == IntPtr.Zero && hWorkerW != IntPtr.Zero);
        }

        return hShellViewWin;
    }
}
```

---

## 4. Ocultación de Iconos del Escritorio

### 4.1 Métodos de Ocultación

Existen 3 métodos principales para ocultar iconos:

#### Método 1: Usando SHELLSTATE (Recomendado para control global)

```csharp
public class DesktopIconManager
{
    /// <summary>
    /// Alterna la visibilidad de TODOS los iconos del escritorio
    /// Usando SHELLSTATE.fHideIcons
    /// </summary>
    public void ToggleAllDesktopIcons()
    {
        Win32API.SHELLSTATE ss = new();
        ss.dwMask = Win32API.SSF_HIDEICONS;
        ss.fHideIcons = true;
        Win32API.SHGetSetSettings(ref ss, Win32API.SSF_HIDEICONS, true);
    }

    /// <summary>
    /// Obtener estado actual de visibilidad de iconos
    /// </summary>
    public bool AreDesktopIconsVisible()
    {
        Win32API.SHELLSTATE ss = new();
        ss.dwMask = Win32API.SSF_HIDEICONS;
        Win32API.SHGetSetSettings(ref ss, Win32API.SSF_HIDEICONS, false);
        return !ss.fHideIcons;
    }

    /// <summary>
    /// Establecer visibilidad explícita
    /// </summary>
    public void SetDesktopIconsVisible(bool visible)
    {
        Win32API.SHELLSTATE ss = new();
        ss.dwMask = Win32API.SSF_HIDEICONS;
        ss.fHideIcons = !visible;
        Win32API.SHGetSetSettings(ref ss, Win32API.SSF_HIDEICONS, true);
    }
}
```

#### Método 2: Envío de Comando WM_COMMAND (Toggle Visual)

```csharp
public void ToggleDesktopIconsVisual()
{
    // Este método envía un comando de toggle al shell
    // Requiere usar SHELLDLL_DefView
    IntPtr hShellView = _desktopWindow.GetShellDefViewHandle();
    if (hShellView == IntPtr.Zero)
        throw new Exception("No se pudo encontrar SHELLDLL_DefView");

    var toggleCommand = new IntPtr(Win32API.TOGGLE_DESKTOP_COMMAND);
    Win32API.SendMessage(hShellView, Win32API.WM_COMMAND, toggleCommand, IntPtr.Zero);
}
```

#### Método 3: Manipulación de Registro (Persistencia)

```csharp
public class RegistryHelper
{
    private const string RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    
    /// <summary>
    /// Leer desde registro si los iconos están ocultos
    /// </summary>
    public static bool AreIconsHiddenInRegistry()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            
            if (key?.GetValue("HideIcons") is int value)
                return value == 1;
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Establecer ocultación en registro
    /// </summary>
    public static void SetIconsHiddenInRegistry(bool hidden)
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);
            
            if (key != null)
            {
                key.SetValue("HideIcons", hidden ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
                key.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error escribiendo registro: {ex.Message}");
        }
    }
}
```

### 4.2 Ocultación Selectiva por Índice

Para ocultar iconos específicos sin afectar a otros:

```csharp
public class SelectiveIconHider
{
    private DesktopWindow _desktopWindow;

    public SelectiveIconHider()
    {
        _desktopWindow = new DesktopWindow();
    }

    /// <summary>
    /// Ocultar un icono específico por su índice
    /// </summary>
    public void HideIconByIndex(int index)
    {
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero)
            throw new Exception("No se pudo localizar el ListView del escritorio");

        // Marcar el elemento como oculto
        Win32API.LVITEM lvItem = new()
        {
            mask = Win32API.LVIF_STATE,
            iItem = index,
            stateMask = Win32API.LVIS_HIDDEN,
            state = Win32API.LVIS_HIDDEN
        };

        IntPtr pLvItem = Marshal.AllocHGlobal(Marshal.SizeOf(lvItem));
        Marshal.StructureToPtr(lvItem, pLvItem, false);

        Win32API.SendMessage(hListView, Win32API.LVM_SETITEM, IntPtr.Zero, pLvItem);
        
        Marshal.FreeHGlobal(pLvItem);
    }

    /// <summary>
    /// Mostrar un icono específico
    /// </summary>
    public void ShowIconByIndex(int index)
    {
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero)
            throw new Exception("No se pudo localizar el ListView del escritorio");

        Win32API.LVITEM lvItem = new()
        {
            mask = Win32API.LVIF_STATE,
            iItem = index,
            stateMask = Win32API.LVIS_HIDDEN,
            state = 0  // Remover bandera de oculto
        };

        IntPtr pLvItem = Marshal.AllocHGlobal(Marshal.SizeOf(lvItem));
        Marshal.StructureToPtr(lvItem, pLvItem, false);

        Win32API.SendMessage(hListView, Win32API.LVM_SETITEM, IntPtr.Zero, pLvItem);
        
        Marshal.FreeHGlobal(pLvItem);
    }

    /// <summary>
    /// Obtener lista de iconos visibles/ocultos
    /// </summary>
    public List<(int Index, string Name, bool IsHidden)> GetDesktopIcons()
    {
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero)
            throw new Exception("No se pudo localizar el ListView del escritorio");

        var result = new List<(int, string, bool)>();
        
        IntPtr countPtr = Win32API.SendMessage(hListView, Win32API.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        int count = countPtr.ToInt32();

        for (int i = 0; i < count; i++)
        {
            // Obtener nombre del icono
            StringBuilder sb = new(260);
            Win32API.LVITEM lvItem = new()
            {
                mask = Win32API.LVIF_TEXT | Win32API.LVIF_STATE,
                iItem = i,
                pszText = Marshal.AllocHGlobal(260),
                cchTextMax = 260,
                stateMask = Win32API.LVIS_HIDDEN
            };

            IntPtr pLvItem = Marshal.AllocHGlobal(Marshal.SizeOf(lvItem));
            Marshal.StructureToPtr(lvItem, pLvItem, false);

            Win32API.SendMessage(hListView, Win32API.LVM_GETITEM, IntPtr.Zero, pLvItem);
            
            lvItem = (Win32API.LVITEM)Marshal.PtrToStructure(pLvItem, typeof(Win32API.LVITEM));
            string name = Marshal.PtrToStringAnsi(lvItem.pszText) ?? $"Icon_{i}";
            bool isHidden = (lvItem.state & Win32API.LVIS_HIDDEN) != 0;

            result.Add((i, name, isHidden));

            Marshal.FreeHGlobal(lvItem.pszText);
            Marshal.FreeHGlobal(pLvItem);
        }

        return result;
    }
}
```

---

## 5. Gestión de Fences (Rejas)

### 5.1 Estructura de Datos de un Fence

```csharp
public class Fence
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public bool IsVisible { get; set; } = true;
    public System.Windows.Rect Bounds { get; set; }
    
    // Iconos contenidos en este fence (guardados por ruta o nombre)
    public List<string> ContainedIconPaths { get; set; } = new();
    
    // Propiedades visuales
    public System.Windows.Media.Color BackgroundColor { get; set; }
    public double Opacity { get; set; } = 0.7;
    public double BorderThickness { get; set; } = 2;
    
    // Configuración
    public bool AllowNavigation { get; set; } = true;
    public bool IsCollapsed { get; set; } = false;
    public string FolderPath { get; set; }  // Para folder portals
    
    // Metadatos
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public int DisplayOrder { get; set; }
}

public class FenceCollection
{
    public List<Fence> Fences { get; set; } = new();
    public List<Snapshot> Snapshots { get; set; } = new();
    public DateTime LastModified { get; set; }
}

public class Snapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public List<Fence> FencesState { get; set; } = new();
    public List<(int Index, int X, int Y)> IconPositions { get; set; } = new();
}
```

### 5.2 Gestor de Fences

```csharp
public class FenceManager
{
    private FenceCollection _collection;
    private DesktopWindow _desktopWindow;
    private const string ConfigFile = "fences_config.json";

    public FenceManager()
    {
        _desktopWindow = new DesktopWindow();
        LoadConfiguration();
    }

    /// <summary>
    /// Crear un nuevo fence
    /// </summary>
    public Fence CreateFence(string name, System.Windows.Rect bounds)
    {
        var fence = new Fence
        {
            Name = name,
            Bounds = bounds,
            DisplayOrder = _collection.Fences.Count
        };
        
        _collection.Fences.Add(fence);
        SaveConfiguration();
        return fence;
    }

    /// <summary>
    /// Eliminar un fence
    /// </summary>
    public void DeleteFence(string fenceId)
    {
        var fence = _collection.Fences.FirstOrDefault(f => f.Id == fenceId);
        if (fence != null)
        {
            _collection.Fences.Remove(fence);
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Actualizar propiedades de un fence
    /// </summary>
    public void UpdateFence(Fence fence)
    {
        var existing = _collection.Fences.FirstOrDefault(f => f.Id == fence.Id);
        if (existing != null)
        {
            existing.Name = fence.Name;
            existing.Bounds = fence.Bounds;
            existing.BackgroundColor = fence.BackgroundColor;
            existing.Opacity = fence.Opacity;
            existing.IsVisible = fence.IsVisible;
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Agregar un icono a un fence (por ruta)
    /// </summary>
    public void AddIconToFence(string fenceId, string iconPath)
    {
        var fence = _collection.Fences.FirstOrDefault(f => f.Id == fenceId);
        if (fence != null && !fence.ContainedIconPaths.Contains(iconPath))
        {
            fence.ContainedIconPaths.Add(iconPath);
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Obtener posición de un icono en el escritorio
    /// </summary>
    public (int X, int Y)? GetIconPosition(string iconName)
    {
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero)
            return null;

        IntPtr countPtr = Win32API.SendMessage(hListView, Win32API.LVM_GETITEMCOUNT, 
                                               IntPtr.Zero, IntPtr.Zero);
        int count = countPtr.ToInt32();

        for (int i = 0; i < count; i++)
        {
            StringBuilder sb = new(260);
            Win32API.LVITEM lvItem = new()
            {
                mask = Win32API.LVIF_TEXT,
                iItem = i,
                pszText = Marshal.AllocHGlobal(260),
                cchTextMax = 260
            };

            IntPtr pLvItem = Marshal.AllocHGlobal(Marshal.SizeOf(lvItem));
            Marshal.StructureToPtr(lvItem, pLvItem, false);
            Win32API.SendMessage(hListView, Win32API.LVM_GETITEM, IntPtr.Zero, pLvItem);
            lvItem = (Win32API.LVITEM)Marshal.PtrToStructure(pLvItem, typeof(Win32API.LVITEM));
            
            string name = Marshal.PtrToStringAnsi(lvItem.pszText) ?? "";
            
            if (name == iconName)
            {
                Win32API.POINT pt = new();
                Win32API.SendMessage(hListView, Win32API.LVM_GETITEMPOSITION, new IntPtr(i), ref pt);
                
                Marshal.FreeHGlobal(lvItem.pszText);
                Marshal.FreeHGlobal(pLvItem);
                
                return (pt.X, pt.Y);
            }

            Marshal.FreeHGlobal(lvItem.pszText);
            Marshal.FreeHGlobal(pLvItem);
        }

        return null;
    }

    /// <summary>
    /// Mover un icono a nuevas coordenadas
    /// </summary>
    public void MoveIcon(int iconIndex, int x, int y)
    {
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero)
            throw new Exception("No se pudo localizar el ListView");

        Win32API.POINT pt = new() { X = x, Y = y };
        Win32API.SendMessage(hListView, Win32API.LVM_SETITEMPOSITION, new IntPtr(iconIndex), ref pt);
    }

    /// <summary>
    /// Crear snapshot de estado actual
    /// </summary>
    public Snapshot CreateSnapshot(string name)
    {
        var snapshot = new Snapshot { Name = name };
        
        // Guardar estado de todos los fences
        snapshot.FencesState = new List<Fence>(_collection.Fences
            .Select(f => new Fence
            {
                Id = f.Id,
                Name = f.Name,
                Bounds = f.Bounds,
                BackgroundColor = f.BackgroundColor,
                IsVisible = f.IsVisible,
                ContainedIconPaths = new List<string>(f.ContainedIconPaths)
            }));

        // Guardar posiciones de iconos
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView != IntPtr.Zero)
        {
            IntPtr countPtr = Win32API.SendMessage(hListView, Win32API.LVM_GETITEMCOUNT, 
                                                   IntPtr.Zero, IntPtr.Zero);
            int count = countPtr.ToInt32();

            for (int i = 0; i < count; i++)
            {
                Win32API.POINT pt = new();
                Win32API.SendMessage(hListView, Win32API.LVM_GETITEMPOSITION, new IntPtr(i), ref pt);
                snapshot.IconPositions.Add((i, pt.X, pt.Y));
            }
        }

        _collection.Snapshots.Add(snapshot);
        SaveConfiguration();
        return snapshot;
    }

    /// <summary>
    /// Restaurar snapshot
    /// </summary>
    public void RestoreSnapshot(string snapshotId)
    {
        var snapshot = _collection.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snapshot == null)
            throw new Exception("Snapshot no encontrado");

        // Restaurar fences
        _collection.Fences = snapshot.FencesState
            .Select(f => new Fence
            {
                Id = f.Id,
                Name = f.Name,
                Bounds = f.Bounds,
                BackgroundColor = f.BackgroundColor,
                IsVisible = f.IsVisible,
                ContainedIconPaths = new List<string>(f.ContainedIconPaths)
            }).ToList();

        // Restaurar posiciones de iconos
        IntPtr hListView = _desktopWindow.GetDesktopListViewHandle();
        if (hListView != IntPtr.Zero)
        {
            foreach (var (index, x, y) in snapshot.IconPositions)
            {
                MoveIcon(index, x, y);
            }
        }

        SaveConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FencesApp");
            
            Directory.CreateDirectory(appDataPath);
            string configPath = Path.Combine(appDataPath, ConfigFile);

            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                _collection = System.Text.Json.JsonSerializer.Deserialize<FenceCollection>(json) 
                              ?? new FenceCollection();
            }
            else
            {
                _collection = new FenceCollection();
            }
        }
        catch
        {
            _collection = new FenceCollection();
        }
    }

    private void SaveConfiguration()
    {
        try
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FencesApp");
            
            Directory.CreateDirectory(appDataPath);
            string configPath = Path.Combine(appDataPath, ConfigFile);

            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            string json = System.Text.Json.JsonSerializer.Serialize(_collection, options);
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando configuración: {ex.Message}");
        }
    }

    public IEnumerable<Fence> GetAllFences() => _collection.Fences.AsReadOnly();
    public IEnumerable<Snapshot> GetAllSnapshots() => _collection.Snapshots.AsReadOnly();
}
```

---

## 6. Persistencia de Datos

### 6.1 Almacenamiento en Registry

```csharp
public class RegistryStorage
{
    private const string FencesRegistryPath = @"HKEY_CURRENT_USER\Software\Stardock\Fences";
    private const string ViewStatesPath = @"HKEY_CURRENT_USER\Software\Stardock\Fences\ViewStates";
    private const string SnapshotsPath = @"HKEY_CURRENT_USER\Software\Stardock\Fences\Snapshots";

    /// <summary>
    /// Guardar configuración de un fence en registro
    /// </summary>
    public static void SaveFenceToRegistry(Fence fence)
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Stardock\Fences\Fences\{fence.Id}");
            
            if (key != null)
            {
                key.SetValue("Name", fence.Name);
                key.SetValue("X", (int)fence.Bounds.X);
                key.SetValue("Y", (int)fence.Bounds.Y);
                key.SetValue("Width", (int)fence.Bounds.Width);
                key.SetValue("Height", (int)fence.Bounds.Height);
                key.SetValue("IsVisible", fence.IsVisible ? 1 : 0);
                key.SetValue("Opacity", fence.Opacity);
                key.SetValue("Color", fence.BackgroundColor.ToString());
                key.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando fence en registro: {ex.Message}");
        }
    }

    /// <summary>
    /// Cargar configuración de fence desde registry
    /// </summary>
    public static Fence LoadFenceFromRegistry(string fenceId)
    {
        var fence = new Fence { Id = fenceId };
        
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                $@"Software\Stardock\Fences\Fences\{fenceId}");
            
            if (key != null)
            {
                fence.Name = (string)key.GetValue("Name", "Fence") ?? "Fence";
                int x = (int)(key.GetValue("X") ?? 0);
                int y = (int)(key.GetValue("Y") ?? 0);
                int width = (int)(key.GetValue("Width") ?? 200);
                int height = (int)(key.GetValue("Height") ?? 200);
                fence.Bounds = new System.Windows.Rect(x, y, width, height);
                fence.IsVisible = ((int)(key.GetValue("IsVisible") ?? 1)) == 1;
                fence.Opacity = (double)(key.GetValue("Opacity") ?? 0.7);
                key.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando fence desde registro: {ex.Message}");
        }

        return fence;
    }

    /// <summary>
    /// Guardar estado de vista de carpeta (para folder portals)
    /// </summary>
    public static void SaveViewState(string folderPath, string viewMode)
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Stardock\Fences\ViewStates");
            
            if (key != null)
            {
                // Crear subkey con hash de ruta de carpeta
                string folderHash = Math.Abs(folderPath.GetHashCode()).ToString("X");
                var folderKey = key.CreateSubKey(folderHash);
                folderKey?.SetValue("Path", folderPath);
                folderKey?.SetValue("ViewMode", viewMode);
                folderKey?.Close();
                key.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando view state: {ex.Message}");
        }
    }

    /// <summary>
    /// Cargar estado de vista de carpeta
    /// </summary>
    public static string LoadViewState(string folderPath)
    {
        try
        {
            string folderHash = Math.Abs(folderPath.GetHashCode()).ToString("X");
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                $@"Software\Stardock\Fences\ViewStates\{folderHash}");
            
            if (key != null)
            {
                string viewMode = (string)(key.GetValue("ViewMode") ?? "Tiles");
                key.Close();
                return viewMode;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando view state: {ex.Message}");
        }

        return "Tiles";
    }
}
```

### 6.2 Almacenamiento en JSON

```csharp
public class JsonStorage
{
    private readonly string _appDataPath;
    private readonly string _fencesDataFile;
    private readonly string _snapshotsDataFile;

    public JsonStorage()
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FencesApp");
        
        Directory.CreateDirectory(_appDataPath);
        
        _fencesDataFile = Path.Combine(_appDataPath, "fences.json");
        _snapshotsDataFile = Path.Combine(_appDataPath, "snapshots.json");
    }

    /// <summary>
    /// Guardar todos los fences
    /// </summary>
    public void SaveFences(List<Fence> fences)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            
            string json = System.Text.Json.JsonSerializer.Serialize(fences, options);
            File.WriteAllText(_fencesDataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando fences: {ex.Message}");
        }
    }

    /// <summary>
    /// Cargar todos los fences
    /// </summary>
    public List<Fence> LoadFences()
    {
        try
        {
            if (File.Exists(_fencesDataFile))
            {
                string json = File.ReadAllText(_fencesDataFile);
                var options = new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                
                return System.Text.Json.JsonSerializer.Deserialize<List<Fence>>(json, options) 
                       ?? new();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando fences: {ex.Message}");
        }

        return new();
    }

    /// <summary>
    /// Guardar snapshots
    /// </summary>
    public void SaveSnapshots(List<Snapshot> snapshots)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            
            string json = System.Text.Json.JsonSerializer.Serialize(snapshots, options);
            File.WriteAllText(_snapshotsDataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando snapshots: {ex.Message}");
        }
    }

    /// <summary>
    /// Cargar snapshots
    /// </summary>
    public List<Snapshot> LoadSnapshots()
    {
        try
        {
            if (File.Exists(_snapshotsDataFile))
            {
                string json = File.ReadAllText(_snapshotsDataFile);
                var options = new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                
                return System.Text.Json.JsonSerializer.Deserialize<List<Snapshot>>(json, options) 
                       ?? new();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando snapshots: {ex.Message}");
        }

        return new();
    }
}
```

---

## 7. Implementación en .NET 10

### 7.1 Estructura del Proyecto

```
FencesApp/
├── FencesApp.csproj
├── Program.cs
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── Core/
│   ├── DesktopWindow.cs
│   ├── DesktopIconManager.cs
│   ├── FenceManager.cs
│   ├── SelectiveIconHider.cs
│   ├── Win32API.cs
│   └── Models/
│       ├── Fence.cs
│       ├── Snapshot.cs
│       └── FenceCollection.cs
├── Storage/
│   ├── RegistryStorage.cs
│   └── JsonStorage.cs
├── UI/
│   ├── FenceWindow.xaml
│   ├── FenceWindow.xaml.cs
│   ├── MainViewModel.cs
│   └── Converters/
│       ├── RectConverter.cs
│       └── ColorConverter.cs
└── Resources/
    └── Styles.xaml
```

### 7.2 Archivo .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.22000.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Runtime.InteropServices" Version="4.3.0" />
  </ItemGroup>

</Project>
```

### 7.3 Program.cs

```csharp
using System.Windows;

namespace FencesApp;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error crítico: {ex.Message}\n{ex.StackTrace}", 
                          "Error de Aplicación", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
```

### 7.4 MainWindow.xaml

```xml
<Window x:Class="FencesApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Fences Manager" Height="600" Width="800"
        Background="#F5F5F5"
        WindowStartupLocation="CenterScreen">
    
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Menu Bar -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Padding="10" Background="#FFFFFF" 
                   BorderThickness="0,0,0,1" BorderBrush="#E0E0E0">
            <Button Content="New Fence" Padding="10,5" Margin="5" Background="#007ACC" 
                   Foreground="White" Click="NewFence_Click"/>
            <Button Content="Hide All Icons" Padding="10,5" Margin="5" Background="#A6221B" 
                   Foreground="White" Click="HideAllIcons_Click"/>
            <Button Content="Show All Icons" Padding="10,5" Margin="5" Background="#3E8E41" 
                   Foreground="White" Click="ShowAllIcons_Click"/>
            <Button Content="Create Snapshot" Padding="10,5" Margin="5" Background="#9370DB" 
                   Foreground="White" Click="CreateSnapshot_Click"/>
        </StackPanel>

        <!-- Fences List -->
        <ListBox Grid.Row="1" x:Name="FencesList" Margin="10" 
                ItemsSource="{Binding Fences}">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Border Padding="10" Background="#FFFFFF" CornerRadius="5" Margin="0,5">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="{Binding Name}" FontSize="14" FontWeight="Bold"/>
                                <TextBlock Text="{Binding Bounds, StringFormat=Pos: {0}}" 
                                          Foreground="#666666" FontSize="11"/>
                                <TextBlock Text="{Binding ContainedIconPaths.Count, 
                                          StringFormat='Contains {0} items'}" 
                                          Foreground="#999999" FontSize="11"/>
                            </StackPanel>

                            <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                                <Button Content="Edit" Padding="8,4" Margin="5" Click="EditFence_Click"/>
                                <Button Content="Delete" Padding="8,4" Margin="5" Background="#A6221B" 
                                       Foreground="White" Click="DeleteFence_Click"/>
                            </StackPanel>
                        </Grid>
                    </Border>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <!-- Status Bar -->
        <StatusBar Grid.Row="2" Background="#F0F0F0" Padding="10">
            <TextBlock x:Name="StatusText" Text="Ready"/>
        </StatusBar>
    </Grid>
</Window>
```

### 7.5 MainWindow.xaml.cs

```csharp
using System.Windows;
using FencesApp.Core;

namespace FencesApp;

public partial class MainWindow : Window
{
    private FenceManager _fenceManager;
    private DesktopIconManager _iconManager;

    public MainWindow()
    {
        InitializeComponent();
        _fenceManager = new FenceManager();
        _iconManager = new DesktopIconManager();
        
        FencesList.ItemsSource = _fenceManager.GetAllFences();
    }

    private void NewFence_Click(object sender, RoutedEventArgs e)
    {
        var fence = _fenceManager.CreateFence("New Fence", new Rect(100, 100, 300, 200));
        StatusText.Text = $"Created fence: {fence.Name}";
        RefreshFencesList();
    }

    private void HideAllIcons_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _iconManager.SetDesktopIconsVisible(false);
            StatusText.Text = "Desktop icons hidden";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Hide Icons Failed");
        }
    }

    private void ShowAllIcons_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _iconManager.SetDesktopIconsVisible(true);
            StatusText.Text = "Desktop icons shown";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Show Icons Failed");
        }
    }

    private void CreateSnapshot_Click(object sender, RoutedEventArgs e)
    {
        string snapshotName = $"Snapshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        _fenceManager.CreateSnapshot(snapshotName);
        StatusText.Text = $"Created snapshot: {snapshotName}";
    }

    private void EditFence_Click(object sender, RoutedEventArgs e)
    {
        // Implementar edición de fence
    }

    private void DeleteFence_Click(object sender, RoutedEventArgs e)
    {
        // Implementar eliminación de fence
    }

    private void RefreshFencesList()
    {
        FencesList.Items.Refresh();
    }
}
```

---

## 8. Referencias y Ejemplos

### 8.1 Proyectos Open Source Relacionados

1. **DesktopFences** (C#)
   - Repositorio: https://github.com/limbo666/DesktopFences
   - Funcionalidad completa para crear fences

2. **Palisades** (.NET 6 + WPF)
   - Repositorio: https://github.com/Xstoudi/Palisades
   - Alternativa open source basada en WPF

### 8.2 APIs Win32 Críticas

| API | Propósito |
|-----|-----------|
| `FindWindow` | Localizar ventanas por clase y nombre |
| `FindWindowEx` | Búsqueda jerárquica de ventanas |
| `SendMessage` | Comunicarse con ventanas |
| `LVM_SETITEMPOSITION` | Mover iconos |
| `LVM_GETITEMPOSITION` | Obtener posición de iconos |
| `LVM_DELETEITEM` | Eliminar iconos |
| `SHGetSetSettings` | Controlar visibilidad global de iconos |
| `RegisterShellHookWindow` | Recibir notificaciones del shell |

### 8.3 Rutas de Registry Importantes

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
├── HideIcons (DWORD) - Ocultar todos los iconos
└── Hidden (DWORD) - Mostrar archivos ocultos

HKCU\Software\Stardock\Fences
├── Fences\{FenceId}
│   ├── Name
│   ├── X, Y, Width, Height
│   └── Color, Opacity
└── ViewStates
    └── {FolderHash}
        ├── Path
        └── ViewMode

HKCR\Local Settings\Software\Microsoft\Windows\Shell
├── BagMRU - Historial de carpetas
└── Bags - Configuración de vistas
```

### 8.4 Mensajes de Ventanas Importantes

```csharp
public const uint
    WM_CREATE = 0x0001,
    WM_DESTROY = 0x0002,
    WM_COMMAND = 0x0111,
    WM_SHOWWINDOW = 0x0018,
    WM_CONTEXTMENU = 0x007B,
    LVM_GETITEMCOUNT = 4100,
    LVM_GETITEM = 4101,
    LVM_SETITEM = 4102,
    LVM_DELETEITEM = 4104,
    LVM_DELETEALLITEMS = 4105,
    LVM_SETITEMPOSITION = 4111,
    LVM_GETITEMPOSITION = 4112,
    LVM_ARRANGE = 4118,
    LVM_FINDITEM = 4179;
```

---

## Conclusión

Este documento proporciona la base completa para implementar una aplicación tipo Fences en .NET 10. La implementación requiere:

1. **Comprender la jerarquía de ventanas** del escritorio de Windows
2. **Dominar P/Invoke** para comunicarse con Win32 API
3. **Gestionar permisos elevados** para acceder al escritorio
4. **Implementar persistencia** en registry y archivos
5. **Crear una UI intuitiva** con WPF o WinUI 3

La complejidad principal radica en la detección fiable del ListView del escritorio y en la manipulación de posiciones de iconos de forma consistente en diferentes versiones de Windows.
