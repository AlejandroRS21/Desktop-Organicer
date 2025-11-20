using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

public class LogsViewModel : INotifyPropertyChanged
{
    private readonly IRepository<FileLog> _logRepository;
    private string _statusMessage = "";

    public ObservableCollection<FileLog> Logs { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ClearLogsCommand { get; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public LogsViewModel(IRepository<FileLog> logRepository)
    {
        _logRepository = logRepository;

        RefreshCommand = new RelayCommand(async _ => await LoadLogsAsync());
        ClearLogsCommand = new RelayCommand(async _ => await ClearLogsAsync());

        // Initial load
        _ = LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        StatusMessage = "Loading logs...";
        Logs.Clear();
        var logs = await _logRepository.GetAllAsync();
        // In a real app, we'd order by date descending here or in the repo query
        foreach (var log in logs)
        {
            Logs.Add(log);
        }
        StatusMessage = "Logs loaded.";
    }

    private async Task ClearLogsAsync()
    {
        // Repository doesn't have DeleteAll, so we'd iterate or add a method.
        // For safety/simplicity, I'll skip implementation or just clear local list for now
        // as GenericRepository only has Delete(entity).
        // Implementing bulk delete is out of scope for this iteration.
        StatusMessage = "Clear logs not implemented in repository yet.";
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
