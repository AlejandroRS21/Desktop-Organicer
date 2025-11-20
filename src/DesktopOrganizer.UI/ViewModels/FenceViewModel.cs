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
    private readonly string _folderPath; // This will now be the Desktop path
    private readonly string[] _extensions; // Extensions to filter
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

    public FenceViewModel(string title, string folderPath, string[] extensions)
    {
        Title = title;
        _folderPath = folderPath;
        _extensions = extensions != null ? extensions.Select(e => e.ToLower()).ToArray() : new string[0];
        
        LoadFiles();
        SetupWatcher();
    }

    private void LoadFiles()
    {
        if (!Directory.Exists(_folderPath)) return;

        var files = Directory.GetFiles(_folderPath);
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            Files.Clear();
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                
                // Filter logic:
                // If extensions list is empty, maybe show everything? No, that would duplicate everything.
                // We only show if it matches our extensions.
                if (_extensions.Contains(ext))
                {
                    Files.Add(new FileItemViewModel
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        Icon = IconHelper.GetIcon(file)
                    });
                }
            }
        });
    }

    private void SetupWatcher()
    {
        if (!Directory.Exists(_folderPath)) return;

        _watcher = new FileSystemWatcher(_folderPath);
        _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite; // Watch for file adds/removes/renames
        _watcher.Created += (s, e) => RefreshFiles();
        _watcher.Deleted += (s, e) => RefreshFiles();
        _watcher.Renamed += (s, e) => RefreshFiles();
        _watcher.EnableRaisingEvents = true;
    }

    private void RefreshFiles()
    {
        // Simple debounce could be added here if needed, but for now direct reload
        Application.Current.Dispatcher.Invoke(() => LoadFiles());
    }

    public void AddFiles(string[] files)
    {
        // When dropping files onto this fence:
        // 1. If the file is NOT on the desktop, move it to the desktop.
        // 2. If it IS on the desktop, do nothing (it's already there).
        // 3. Ideally, we should check if the file extension matches this fence. If not, maybe warn? 
        //    But for now, let's just move it to Desktop.

        foreach (var file in files)
        {
            if (!File.Exists(file) && !Directory.Exists(file)) continue;

            try
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(_folderPath, fileName); // _folderPath is Desktop

                // Check if source and dest are same
                if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Already on desktop
                }

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
                Console.WriteLine($"Error moving file: {ex.Message}");
            }
        }
        // Watcher will trigger refresh
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}
