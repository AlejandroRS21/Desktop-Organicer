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

    public SettingsViewModel(IRepository<UserPreferences> prefsRepository)
    {
        _prefsRepository = prefsRepository;
        _preferences = new UserPreferences(); // Default

        SaveCommand = new RelayCommand(async _ => await SavePreferencesAsync());

        _ = LoadPreferencesAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        StatusMessage = "Loading settings...";
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
        StatusMessage = "Settings loaded.";
    }

    private async Task SavePreferencesAsync()
    {
        if (_preferences != null)
        {
            await _prefsRepository.UpdateAsync(_preferences);
            StatusMessage = "Settings saved.";
        }
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
