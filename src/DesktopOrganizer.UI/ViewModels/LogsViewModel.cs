using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly IRepository<FileLog> _logRepository;

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<FileLog> Logs { get; } = new();

    public LogsViewModel(IRepository<FileLog> logRepository)
    {
        _logRepository = logRepository;
        // Initial load
        _ = LoadLogs();
    }

    [RelayCommand]
    private async Task LoadLogs()
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

    [RelayCommand]
    private async Task ClearLogs()
    {
        // Repository doesn't have DeleteAll, so we'd iterate or add a method.
        // For safety/simplicity, I'll skip implementation or just clear local list for now
        // as GenericRepository only has Delete(entity).
        // Implementing bulk delete is out of scope for this iteration.
        StatusMessage = "Clear logs not implemented in repository yet.";
    }
}
