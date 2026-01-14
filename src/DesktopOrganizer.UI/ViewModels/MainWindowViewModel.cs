using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace DesktopOrganizer.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileWatcher _fileWatcher;
    private readonly FileOrganizer _fileOrganizer;
    private readonly System.Func<RuleEditorView> _ruleEditorFactory;
    private readonly System.Func<LogsView> _logsViewFactory;
    private readonly System.Func<SettingsView> _settingsViewFactory;
    private readonly IRepository<UserPreferences> _prefsRepository;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopButtonText))]
    private bool _isMonitoring;

    public string StartStopButtonText => IsMonitoring ? "Stop Monitoring" : "Start Monitoring";

    [ObservableProperty]
    private ObservableCollection<FenceConfiguration> _fences;

    private readonly DesktopOrganizer.UI.Services.FenceManager _fenceManager;

    public MainWindowViewModel(
        IFileWatcher fileWatcher, 
        FileOrganizer fileOrganizer, 
        System.Func<RuleEditorView> ruleEditorFactory,
        System.Func<LogsView> logsViewFactory,
        System.Func<SettingsView> settingsViewFactory,
        IRepository<UserPreferences> prefsRepository,
        DesktopOrganizer.UI.Services.FenceManager fenceManager)
    {
        _fileWatcher = fileWatcher;
        _fileOrganizer = fileOrganizer;
        _ruleEditorFactory = ruleEditorFactory;
        _logsViewFactory = logsViewFactory;
        _settingsViewFactory = settingsViewFactory;
        _prefsRepository = prefsRepository;
        _fenceManager = fenceManager;
        
        // Subscribe to events
        _fileWatcher.FileCreated += OnFileDetected;
        _fenceManager.FencesUpdated += () => System.Windows.Application.Current.Dispatcher.Invoke(InitializeAsync);

        // Load rules and fences
        InitializeAsync();
    }

    [RelayCommand]
    private void OpenRuleEditor()
    {
        var ruleEditor = _ruleEditorFactory();
        ruleEditor.Show();
    }

    [RelayCommand]
    private void OpenLogs()
    {
        var logsView = _logsViewFactory();
        logsView.Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsView = _settingsViewFactory();
        settingsView.Show();
    }

    [RelayCommand]
    private void SaveRules(FenceConfiguration fence)
    {
        if (fence != null)
        {
            try
            {
                _fenceManager.UpdateFenceRules(fence.Id, fence.Extensions);
                StatusMessage = $"Reglas actualizadas para {fence.Name}";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error guardando reglas: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task SortDesktop()
    {
        try
        {
            StatusMessage = "Sorting desktop...";
            var desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            var files = System.IO.Directory.GetFiles(desktopPath);
            
            int count = 0;
            foreach (var file in files)
            {
                // Skip hidden files or system files if needed, but Organizer handles valid files
                await _fileOrganizer.OrganizeFileAsync(file);
                count++;
            }
            
            StatusMessage = $"Sorted {count} files.";
            
            // Allow fences to refresh
            // They watch FS so they should update automatically if files are moved/renamed
            // But if moved to folders, they disappear from fences.
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error sorting: {ex.Message}";
        }
    }

    private async void InitializeAsync()
    {
        try
        {
            StatusMessage = "Loading rules and fences...";
            await _fileOrganizer.LoadRulesAsync();
            
            // Load fences on UI thread
            var fences = _fenceManager.GetAllFences();
            Fences = new ObservableCollection<FenceConfiguration>(fences);
            
            StatusMessage = "Ready";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error loading: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleMonitoring()
    {
        if (IsMonitoring)
        {
            _fileWatcher.StopMonitoring();
            StatusMessage = "Monitoring stopped.";
            IsMonitoring = false;
        }
        else
        {
            try
            {
                // Get path from preferences
                var prefs = (await _prefsRepository.GetAllAsync()).FirstOrDefault();
                string path = prefs?.MonitoredDirectories ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                
                // Handle multiple paths if semicolon separated (just take first for now as FileWatcherService supports one)
                if (path.Contains(";"))
                {
                    path = path.Split(';')[0];
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                     path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                }

                _fileWatcher.StartMonitoring(path);
                StatusMessage = $"Monitoring started on {path}";
                IsMonitoring = true;
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }

    private async void OnFileDetected(object? sender, string filePath)
    {
        StatusMessage = $"Detected new file: {System.IO.Path.GetFileName(filePath)}";
        await _fileOrganizer.OrganizeFileAsync(filePath);
    }
}
