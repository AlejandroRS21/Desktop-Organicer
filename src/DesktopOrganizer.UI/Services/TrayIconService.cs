using System;
using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;
using System.Windows;

namespace DesktopOrganizer.UI.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly Func<Window> _settingsWindowFactory;
    private readonly FenceManager _fenceManager;
    private readonly DesktopOrganizer.Core.Services.FileOrganizer _fileOrganizer; // Added
    private Window? _currentSettingsWindow;

    public TrayIconService(
        Func<Window> settingsWindowFactory, 
        FenceManager fenceManager, 
        DesktopOrganizer.Core.Services.FileOrganizer fileOrganizer) // Added
    {
        _settingsWindowFactory = settingsWindowFactory;
        _fenceManager = fenceManager;
        _fileOrganizer = fileOrganizer; // Added
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        var icon = CreateAppIcon();
        
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = "Desktop Organizer"
        };

        var contextMenu = new ContextMenuStrip();
        
        var settingsItem = new ToolStripMenuItem("⚙️ Configuración");
        settingsItem.Click += (s, e) => OpenSettings();
        
        var createFenceItem = new ToolStripMenuItem("➕ Crear Nuevo Fence");
        createFenceItem.Click += (s, e) => CreateNewFence();
        
        var toggleFencesItem = new ToolStripMenuItem("👁️ Mostrar/Ocultar Fences");
        toggleFencesItem.Click += (s, e) => ToggleFences();

        var toggleDesktopIconsItem = new ToolStripMenuItem("🖥️ Ocultar/Mostrar Iconos Escritorio");
        toggleDesktopIconsItem.Click += (s, e) => ToggleDesktopIcons();

        var sortDesktopItem = new ToolStripMenuItem("🧹 Ordenar Escritorio");
        sortDesktopItem.Click += (s, e) => SortDesktopNow();

        var refreshItem = new ToolStripMenuItem("🔄 Recargar Fences");
        refreshItem.Click += (s, e) => RefreshFences();

        var exitItem = new ToolStripMenuItem("❌ Salir");
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(createFenceItem);
        contextMenu.Items.Add(toggleFencesItem);
        contextMenu.Items.Add(toggleDesktopIconsItem);
        contextMenu.Items.Add(sortDesktopItem); // Added
        contextMenu.Items.Add(refreshItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => OpenSettings();
    }

    private void CreateNewFence()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Create a new fence in the center of the screen
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;
            
            double width = 220;
            double height = 280;
            double left = (screenWidth - width) / 2;
            double top = (screenHeight - height) / 2;
            
            _fenceManager.CreateCustomFence(left, top, width, height);
            
            ShowBalloon("Fence Creado", "Se ha creado un nuevo fence. Puedes arrastrarlo y redimensionarlo.");
        });
    }

    private void ShowBalloon(string title, string text)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
    }

    private Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        
        g.Clear(Color.FromArgb(139, 92, 246));
        
        using var brush = new SolidBrush(Color.White);
        g.FillRectangle(brush, 2, 2, 5, 5);
        g.FillRectangle(brush, 9, 2, 5, 5);
        g.FillRectangle(brush, 2, 9, 5, 5);
        g.FillRectangle(brush, 9, 9, 5, 5);
        
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OpenSettings()
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_currentSettingsWindow != null && _currentSettingsWindow.IsLoaded)
                    {
                        _currentSettingsWindow.Activate();
                        if (_currentSettingsWindow.WindowState == WindowState.Minimized)
                            _currentSettingsWindow.WindowState = WindowState.Normal;
                        return;
                    }

                    var window = _settingsWindowFactory();
                    _currentSettingsWindow = window;
                    
                    window.Closed += (s, e) => _currentSettingsWindow = null;
                    window.Show();
                    window.Activate();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error abriendo configuración: {ex.Message}\n\n{ex.StackTrace}", 
                        "Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OpenSettings: {ex}");
        }
    }

    private void ToggleFences()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _fenceManager.ToggleFencesVisibility();
        });
    }

    private void ToggleDesktopIcons()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                Helpers.DesktopIconToggler.ToggleDesktopIcons();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling desktop icons: {ex.Message}");
            }
        });
    }

    private void RefreshFences()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _fenceManager.InitializeFences();
        });
    }

    private void ExitApplication()
    {
        try
        {
            Helpers.DesktopIconToggler.ShowDesktopIcons();
        }
        catch { }
        
        if (_notifyIcon != null)
            _notifyIcon.Visible = false;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
