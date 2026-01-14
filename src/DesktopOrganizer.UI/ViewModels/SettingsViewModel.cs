using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DesktopOrganizer.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IRepository<UserPreferences> _prefsRepository;
    private UserPreferences _preferences;
    
    [ObservableProperty]
    private string _statusMessage = "";

    public string MonitoredDirectories
    {
        get => _preferences?.MonitoredDirectories ?? "";
        set
        {
            if (_preferences != null && _preferences.MonitoredDirectories != value)
            {
                _preferences.MonitoredDirectories = value;
                OnPropertyChanged();
            }
        }
    }

    public double FenceOpacity
    {
        get => _preferences?.FenceOpacity ?? 0.6;
        set
        {
            if (_preferences != null && _preferences.FenceOpacity != value)
            {
                _preferences.FenceOpacity = value;
                OnPropertyChanged();
                _fenceManager.UpdateFenceAppearance(_preferences.FenceColorHex, value);
            }
        }
    }

    public string FenceColorHex
    {
        get => _preferences?.FenceColorHex ?? "#1E293B";
        set
        {
            if (_preferences != null && _preferences.FenceColorHex != value)
            {
                _preferences.FenceColorHex = value;
                OnPropertyChanged();
                _fenceManager.UpdateFenceAppearance(value, _preferences.FenceOpacity);
            }
        }
    }

    public bool EnableFenceBlur
    {
        get => _preferences?.EnableFenceBlur ?? true;
        set
        {
            if (_preferences != null && _preferences.EnableFenceBlur != value)
            {
                _preferences.EnableFenceBlur = value;
                OnPropertyChanged();
            }
        }
    }

    private readonly DesktopOrganizer.UI.Services.FenceManager _fenceManager;
    private readonly DesktopOrganizer.UI.Services.ThemeManager _themeManager;
    private readonly System.Func<Views.RuleEditorView> _ruleEditorFactory;

    public SettingsViewModel(IRepository<UserPreferences> prefsRepository, 
                             DesktopOrganizer.UI.Services.FenceManager fenceManager,
                             DesktopOrganizer.UI.Services.ThemeManager themeManager,
                             System.Func<Views.RuleEditorView> ruleEditorFactory)
    {
        _prefsRepository = prefsRepository;
        _fenceManager = fenceManager;
        _themeManager = themeManager;
        _ruleEditorFactory = ruleEditorFactory;
        _preferences = new UserPreferences(); // Default

        _ = LoadPreferencesAsync();
    }

    [RelayCommand]
    private void OpenRuleEditor()
    {
        try
        {
            var window = _ruleEditorFactory();
            window.Show();
            window.Activate();
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error abriendo editor: {ex.Message}";
        }
    }

    public int ThemeMode
    {
        get => _preferences?.ThemeMode ?? 0;
        set
        {
            if (_preferences != null && _preferences.ThemeMode != value)
            {
                _preferences.ThemeMode = value;
                OnPropertyChanged();
                _themeManager.ApplyTheme((DesktopOrganizer.UI.Services.AppTheme)value);
            }
        }
    }

    private async Task LoadPreferencesAsync()
    {
        StatusMessage = "Cargando configuración...";
        var prefs = (await _prefsRepository.GetAllAsync()).FirstOrDefault();
        if (prefs == null)
        {
            // Create default
            prefs = new UserPreferences
            {
                MonitoredDirectories = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                ThemeMode = 0 // Default Light
            };
            await _prefsRepository.AddAsync(prefs);
        }
        
        _preferences = prefs;
        OnPropertyChanged(nameof(MonitoredDirectories));
        OnPropertyChanged(nameof(FenceOpacity));
        OnPropertyChanged(nameof(FenceColorHex));
        OnPropertyChanged(nameof(EnableFenceBlur));
        OnPropertyChanged(nameof(ThemeMode));
        
        // Apply theme
        _themeManager.ApplyTheme((DesktopOrganizer.UI.Services.AppTheme)_preferences.ThemeMode);
        
        StatusMessage = "Configuración cargada.";
    }

    [RelayCommand]
    private async Task SavePreferences()
    {
        if (_preferences != null)
        {
            await _prefsRepository.UpdateAsync(_preferences);
            
            // Reload fences to apply changes
            StatusMessage = "Guardando y aplicando cambios...";
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                _fenceManager.InitializeFences();
            });

            StatusMessage = "Configuración guardada y Fences actualizados.";
        }
    }
}
