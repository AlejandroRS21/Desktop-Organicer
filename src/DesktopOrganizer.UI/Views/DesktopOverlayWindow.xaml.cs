using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopOrganizer.UI.Helpers;
using DesktopOrganizer.UI.ViewModels;
using DesktopOrganizer.UI.Services;

namespace DesktopOrganizer.UI.Views;

public partial class DesktopOverlayWindow : Window
{
    private Point _dragStart;
    private bool _isDrawing = false;
    private readonly DesktopIconManager _iconManager = new DesktopIconManager();
    private readonly FenceManager _fenceManager;

    private DesktopIconControl? _draggedIcon;
    private Point _iconDragOffset;

    public DesktopOverlayWindow(FenceManager fenceManager)
    {
        InitializeComponent();
        _fenceManager = fenceManager;
        
        // Setup Fullscreen to cover virtual screen
        this.Left = SystemParameters.VirtualScreenLeft;
        this.Top = SystemParameters.VirtualScreenTop;
        this.Width = SystemParameters.VirtualScreenWidth;
        this.Height = SystemParameters.VirtualScreenHeight;
        
        this.Loaded += (s, e) => {
            try {
                // IMPORTANT: Restore SetToDesktop to prevent taskbar blockage
                DesktopWindowHelper.SetToDesktop(this);
                DesktopWindowHelper.SendToBottom(this);
                LoadIcons();
            } catch (Exception ex) {
                 System.Diagnostics.Debug.WriteLine($"Overlay Load Error: {ex.Message}");
            }
        };
    }

    private void LoadIcons()
    {
        IconCanvas.Children.Clear();
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!Directory.Exists(desktopPath)) return;

        var files = Directory.GetFiles(desktopPath)
            .Concat(Directory.GetDirectories(desktopPath))
            .ToList();
        
        double x = 20;
        double y = 20;
        double columnWidth = 100;
        double rowHeight = 110;
        
        foreach (var file in files)
        {
            try
            {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(fileName)) continue;

                var icon = IconHelper.GetIcon(file);
                
                var control = new DesktopIconControl();
                // Store path in DataContext
                control.DataContext = new { Name = fileName, Icon = icon, FullPath = file };
                control.Tag = file;
                control.MouseLeftButtonDown += Icon_MouseLeftButtonDown;
                
                Canvas.SetLeft(control, x);
                Canvas.SetTop(control, y);
                IconCanvas.Children.Add(control);
                
                y += rowHeight;
                if (y + rowHeight > this.Height - 100)
                {
                    y = 20;
                    x += columnWidth;
                }
            }
            catch {}
        }
    }

    private void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DesktopIconControl control)
        {
            if (e.ClickCount == 2)
            {
                // OPEN FILE
                var path = control.Tag as string;
                if (!string.IsNullOrEmpty(path))
                {
                    try {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    } catch (Exception ex) {
                        MessageBox.Show($"No se pudo abrir: {ex.Message}");
                    }
                }
                return;
            }

            _draggedIcon = control;
            _iconDragOffset = e.GetPosition(control);
            control.CaptureMouse();
            e.Handled = true;
        }
    }

    private void IconCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Debugging click
        System.Diagnostics.Debug.WriteLine($"Canvas Down at {e.GetPosition(this)}");

        _dragStart = e.GetPosition(IconCanvas);
        _isDrawing = true;
        
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _dragStart.X);
        Canvas.SetTop(SelectionRect, _dragStart.Y);
        
        // Debug
        System.Diagnostics.Debug.WriteLine($"Drawing start at: {_dragStart}");
    }

    private void IconCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        Point current = e.GetPosition(IconCanvas);

        if (_draggedIcon != null)
        {
            double newX = current.X - _iconDragOffset.X;
            double newY = current.Y - _iconDragOffset.Y;
            Canvas.SetLeft(_draggedIcon, newX);
            Canvas.SetTop(_draggedIcon, newY);
            return;
        }

        if (!_isDrawing) return;
        
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
        if (_draggedIcon != null)
        {
            _draggedIcon.ReleaseMouseCapture();
            
            Point dropPoint = e.GetPosition(this);
            Point screenPoint = this.PointToScreen(dropPoint);
            
            // CHECK DROP INTO FENCE
            var filePath = _draggedIcon.Tag as string;
            if (!string.IsNullOrEmpty(filePath))
            {
                var fenceWindow = _fenceManager.GetFenceWindowAtPoint(screenPoint);
                if (fenceWindow != null && fenceWindow.DataContext is FenceViewModel vm)
                {
                    // Call the logic to add file (which moves it on disk)
                    vm.AddFiles(new[] { filePath });
                    IconCanvas.Children.Remove(_draggedIcon);
                }
            }
            
            _draggedIcon = null;
            return;
        }

        if (!_isDrawing) return;
        _isDrawing = false;
        
        Point end = e.GetPosition(IconCanvas);
        SelectionRect.Visibility = Visibility.Collapsed;
        
        double width = Math.Abs(end.X - _dragStart.X);
        double height = Math.Abs(end.Y - _dragStart.Y);
        
        if (width > 40 && height > 40)
        {
            double x = Math.Min(_dragStart.X, end.X);
            double y = Math.Min(_dragStart.Y, end.Y);
            
            _fenceManager.CreateCustomFence(x + this.Left, y + this.Top, width, height);
        }
    }
}
