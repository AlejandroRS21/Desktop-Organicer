using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Linq;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.Views;

namespace DesktopOrganizer.UI.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFileWatcher _fileWatcher;
    private readonly FileOrganizer _fileOrganizer;
    private readonly System.Func<RuleEditorView> _ruleEditorFactory;
    private readonly System.Func<LogsView> _logsViewFactory;
    private readonly System.Func<SettingsView> _settingsViewFactory;
    private readonly IRepository<UserPreferences> _prefsRepository;
    
    private string _statusMessage = "Ready";
    private bool _isMonitoring;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        set
        {
            _isMonitoring = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartStopButtonText));
        }
    }

    public string StartStopButtonText => IsMonitoring ? "Stop Monitoring" : "Start Monitoring";

    public ICommand ToggleMonitoringCommand { get; }
    public ICommand OpenRuleEditorCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand SortDesktopCommand { get; }

    private ObservableCollection<FenceConfiguration> _fences;
    private readonly DesktopOrganizer.UI.Services.FenceManager _fenceManager;

    public ObservableCollection<FenceConfiguration> Fences
    {
        get => _fences;
        set
        {
            _fences = value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveRulesCommand { get; }

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
        
        ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
        OpenRuleEditorCommand = new RelayCommand(OpenRuleEditor);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        SaveRulesCommand = new RelayCommand(SaveFenceRules);
        SortDesktopCommand = new RelayCommand(SortDesktop);
        
        // Subscribe to events
        _fileWatcher.FileCreated += OnFileDetected;
        _fenceManager.FencesUpdated += () => System.Windows.Application.Current.Dispatcher.Invoke(InitializeAsync);

        // Load rules and fences
        InitializeAsync();
    }

    private void OpenRuleEditor(object? parameter)
    {
        var ruleEditor = _ruleEditorFactory();
        ruleEditor.Show();
    }

    private void OpenLogs(object? parameter)
    {
        var logsView = _logsViewFactory();
        logsView.Show();
    }

    private void OpenSettings(object? parameter)
    {
        var settingsView = _settingsViewFactory();
        settingsView.Show();
    }

    private void SaveFenceRules(object? parameter)
    {
        if (parameter is FenceConfiguration fence)
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

    private async void SortDesktop(object? parameter)
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

    private async void ToggleMonitoring(object? parameter)
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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Simple RelayCommand implementation
public class RelayCommand : ICommand
{
    private readonly System.Action<object?> _execute;
    private readonly System.Func<object?, bool>? _canExecute;

    public RelayCommand(System.Action<object?> execute, System.Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    public event System.EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
