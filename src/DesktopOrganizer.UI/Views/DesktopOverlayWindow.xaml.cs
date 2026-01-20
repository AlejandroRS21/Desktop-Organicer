using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopOrganizer.UI.Helpers;
using DesktopOrganizer.UI.ViewModels;
using DesktopOrganizer.UI.Services;

namespace DesktopOrganizer.UI.Views;

public partial class DesktopOverlayWindow : Window
{
    #region P/Invoke for Z-Order Control
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetCursorPos(out POINT lpPoint);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOACTIVATE = 0x0010;

    private const int WM_WINDOWPOSCHANGING = 0x0046;
    
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }
    #endregion

    private Point _dragStart;
    private bool _isDrawing = false;
    private readonly FenceManager _fenceManager;
    private readonly DesktopIconManager _iconManager;

    private DesktopIconControl? _draggedIcon;
    private IntPtr _hwnd;

    public DesktopOverlayWindow(FenceManager fenceManager, DesktopIconManager iconManager)
    {
        InitializeComponent();
        _fenceManager = fenceManager;
        _iconManager = iconManager;
        
        // Use WORKING AREA to exclude taskbar
        // This prevents blocking the taskbar
        this.Left = SystemParameters.WorkArea.Left;
        this.Top = SystemParameters.WorkArea.Top;
        this.Width = SystemParameters.WorkArea.Width;
        this.Height = SystemParameters.WorkArea.Height;
        
        this.Loaded += OnLoaded;
        this.Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        
        // Add WndProc hook to intercept window messages
        HwndSource source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
        
        // Send to bottom of Z-order
        ForceToBottom();
        
        // Hide native icons and load simulated ones
        _iconManager.HideAllIcons();
        LoadIconsFromNativePositions();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Restore native icons when closing
        _iconManager.ShowAllIcons();
    }

    private void ForceToBottom()
    {
        SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            // Force window to stay at bottom Z-order
            var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            wp.hwndInsertAfter = HWND_BOTTOM;
            wp.flags |= 0x0010; // SWP_NOACTIVATE
            Marshal.StructureToPtr(wp, lParam, false);
        }
        return IntPtr.Zero;
    }

    private void LoadIconsFromNativePositions()
    {
        IconCanvas.Children.Clear();
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!Directory.Exists(desktopPath)) return;

        // Get native icon positions from DesktopIconManager
        var nativePositions = _iconManager.GetAllIconPositions();
        
        var files = Directory.GetFiles(desktopPath)
            .Concat(Directory.GetDirectories(desktopPath))
            .ToList();

        foreach (var file in files)
        {
            try
            {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(fileName)) continue;
                
                // Skip hidden/system files
                var attr = File.GetAttributes(file);
                if ((attr & FileAttributes.Hidden) != 0 || (attr & FileAttributes.System) != 0)
                    continue;

                var icon = IconHelper.GetIcon(file);
                
                var control = new DesktopIconControl();
                control.DataContext = new { Name = fileName, Icon = icon, FullPath = file };
                control.Tag = file;
                control.MouseLeftButtonDown += Icon_MouseLeftButtonDown;
                control.MouseMove += Icon_MouseMove;
                control.MouseLeftButtonUp += Icon_MouseLeftButtonUp;
                control.MouseEnter += Icon_MouseEnter;
                control.MouseLeave += Icon_MouseLeave;
                
                // Try to get native position, otherwise use grid layout
                double x = 20, y = 20;
                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                
                if (nativePositions.TryGetValue(fileName, out var pos))
                {
                    // Adjust for working area offset
                    x = pos.X - SystemParameters.WorkArea.Left;
                    y = pos.Y - SystemParameters.WorkArea.Top;
                }
                else if (nativePositions.TryGetValue(nameNoExt, out pos))
                {
                    x = pos.X - SystemParameters.WorkArea.Left;
                    y = pos.Y - SystemParameters.WorkArea.Top;
                }
                else
                {
                    // Fallback: grid layout
                    x = 20 + (IconCanvas.Children.Count % 10) * 90;
                    y = 20 + (IconCanvas.Children.Count / 10) * 100;
                }
                
                Canvas.SetLeft(control, x);
                Canvas.SetTop(control, y);
                IconCanvas.Children.Add(control);
            }
            catch { }
        }
    }

    private void Icon_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is DesktopIconControl control)
        {
            control.Background = new SolidColorBrush(Color.FromArgb(0x44, 0x00, 0x78, 0xD7));
            control.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0x00, 0x78, 0xD7),
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.7
            };
        }
    }

    private void Icon_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is DesktopIconControl control && control != _draggedIcon)
        {
            control.Background = Brushes.Transparent;
            control.Effect = null;
        }
    }

    private void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DesktopIconControl control)
        {
            if (e.ClickCount == 2)
            {
                // Double-click: Open file
                var path = control.Tag as string;
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"No se pudo abrir: {ex.Message}");
                    }
                }
                e.Handled = true;
                return;
            }

            // Start WPF DragDrop operation - this handles Z-order properly
            _draggedIcon = control;
            var filePath = control.Tag as string;
            if (!string.IsNullOrEmpty(filePath))
            {
                // Create DataObject with file path
                DataObject data = new DataObject(DataFormats.FileDrop, new string[] { filePath });
                
                // Also add a custom format for internal handling
                data.SetData("DesktopIconDrag", filePath);
                
                // Perform the drag - this is blocking and handles everything
                DragDrop.DoDragDrop(control, data, DragDropEffects.Move | DragDropEffects.Copy);
                
                // After drop: Check if the file still exists on the desktop
                // If it's gone, it means it was successfully moved to a fence
                if (!File.Exists(filePath) && !Directory.Exists(filePath))
                {
                    IconCanvas.Children.Remove(control);
                }
            }
            
            _draggedIcon = null;
            e.Handled = true;
        }
    }

    private void Icon_MouseMove(object sender, MouseEventArgs e)
    {
        // Not used anymore - DragDrop handles movement
    }

    private void Icon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Not used anymore - DragDrop handles drop
        _draggedIcon = null;
    }

    private void IconCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only start fence drawing if not clicking an icon
        if (e.OriginalSource == IconCanvas || e.OriginalSource is Canvas)
        {
            _dragStart = e.GetPosition(IconCanvas);
            _isDrawing = true;
            
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRect, _dragStart.X);
            Canvas.SetTop(SelectionRect, _dragStart.Y);
        }
    }

    private void IconCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        
        Point current = e.GetPosition(IconCanvas);
        double x = Math.Min(_dragStart.X, current.X);
        double y = Math.Min(_dragStart.Y, current.Y);
        double width = Math.Abs(current.X - _dragStart.X);
        double height = Math.Abs(current.Y - _dragStart.Y);
        
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;
    }

    private void IconCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        
        Point end = e.GetPosition(IconCanvas);
        SelectionRect.Visibility = Visibility.Collapsed;
        
        double width = Math.Abs(end.X - _dragStart.X);
        double height = Math.Abs(end.Y - _dragStart.Y);
        
        // Only create fence if rectangle is large enough
        if (width > 50 && height > 50)
        {
            double x = Math.Min(_dragStart.X, end.X) + this.Left;
            double y = Math.Min(_dragStart.Y, end.Y) + this.Top;
            
            _fenceManager.CreateCustomFence(x, y, width, height);
        }
    }
}
