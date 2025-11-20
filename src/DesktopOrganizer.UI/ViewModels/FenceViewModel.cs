using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using DesktopOrganizer.UI.Helpers;

namespace DesktopOrganizer.UI.ViewModels;

public class FenceViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _title;
    private string _folderPath;
    private FileSystemWatcher _watcher;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public ObservableCollection<FileItemViewModel> Files { get; } = new ObservableCollection<FileItemViewModel>();

    public FenceViewModel(string title, string folderPath)
    {
        Title = title;
        _folderPath = folderPath;
        LoadFiles();
        SetupWatcher();
    }

    private void LoadFiles()
    {
        if (!Directory.Exists(_folderPath)) return;

        // Get both files and directories
        var entries = Directory.GetFileSystemEntries(_folderPath);
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            Files.Clear();
            foreach (var entry in entries)
            {
                // Skip hidden files/folders if necessary, but for now show all
                Files.Add(new FileItemViewModel
                {
                    Name = Path.GetFileName(entry),
                    FullPath = entry,
                    Icon = IconHelper.GetIcon(entry)
                });
            }
        });
    }

    private void SetupWatcher()
    {
        if (!Directory.Exists(_folderPath)) return;

        _watcher = new FileSystemWatcher(_folderPath);
        _watcher.Created += (s, e) => RefreshFiles();
        _watcher.Deleted += (s, e) => RefreshFiles();
        _watcher.Renamed += (s, e) => RefreshFiles();
        _watcher.EnableRaisingEvents = true;
    }

    private void RefreshFiles()
    {
        // Debounce or just reload
        Application.Current.Dispatcher.Invoke(() => LoadFiles());
    }

    public void AddFiles(string[] files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(file) && !Directory.Exists(file)) continue;

            try
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(_folderPath, fileName);

                // Handle duplicate names
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    destPath = Path.Combine(_folderPath, $"{nameWithoutExt}_{timestamp}{ext}");
                }

                if (Directory.Exists(file))
                {
                    Directory.Move(file, destPath);
                }
                else
                {
                    File.Move(file, destPath);
                }
            }
            catch (Exception ex)
            {
                // Log or show error?
                Console.WriteLine($"Error moving file: {ex.Message}");
            }
        }
        // Refresh will happen automatically via FileSystemWatcher
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}
