using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IRepository<UserPreferences> _prefsRepository;
    private UserPreferences _preferences;
    private string _statusMessage = "";

    public string MonitoredDirectories
    {
        get => _preferences?.MonitoredDirectories ?? "";
        set
        {
            if (_preferences != null)
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
            if (_preferences != null)
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
            if (_preferences != null)
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
            if (_preferences != null)
            {
                _preferences.EnableFenceBlur = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }
    public ICommand SaveCommand { get; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private readonly DesktopOrganizer.UI.Services.FenceManager _fenceManager;

    public SettingsViewModel(IRepository<UserPreferences> prefsRepository, DesktopOrganizer.UI.Services.FenceManager fenceManager)
    {
        _prefsRepository = prefsRepository;
        _fenceManager = fenceManager;
        _preferences = new UserPreferences(); // Default

        SaveCommand = new RelayCommand(async _ => await SavePreferencesAsync());

        _ = LoadPreferencesAsync();
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
                MonitoredDirectories = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)
            };
            await _prefsRepository.AddAsync(prefs);
        }
        
        _preferences = prefs;
        OnPropertyChanged(nameof(MonitoredDirectories));
        OnPropertyChanged(nameof(FenceOpacity));
        OnPropertyChanged(nameof(FenceColorHex));
        OnPropertyChanged(nameof(EnableFenceBlur));
        StatusMessage = "Configuración cargada.";
    }

    private async Task SavePreferencesAsync()
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

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
