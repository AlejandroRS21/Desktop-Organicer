using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Services;

namespace DesktopOrganizer.UI.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFileWatcher _fileWatcher;
    private readonly FileOrganizer _fileOrganizer;
    
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

    public MainWindowViewModel(IFileWatcher fileWatcher, FileOrganizer fileOrganizer)
    {
        _fileWatcher = fileWatcher;
        _fileOrganizer = fileOrganizer;
        
        ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
        
        // Subscribe to events
        _fileWatcher.FileCreated += OnFileDetected;
    }

    private void ToggleMonitoring(object? parameter)
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
                string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                _fileWatcher.StartMonitoring(desktopPath);
                StatusMessage = $"Monitoring started on {desktopPath}";
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
}
