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
    private string _title = string.Empty;
    private readonly string _folderPath; // This will now be the Desktop path
    private readonly string[] _extensions; // Extensions to filter
    private FileSystemWatcher _watcher = null!;

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

    public event EventHandler? FilesChanged;
    
    // Style Properties
    private string _hexColor = "#CC1E293B";
    private double _opacity = 0.85;
    
    public string HexColor 
    { 
        get => _hexColor;
        set
        {
            _hexColor = value;
            OnPropertyChanged(nameof(HexColor));
            OnPropertyChanged(nameof(HeaderColor));
        }
    }
    
    public double Opacity 
    { 
        get => _opacity;
        set
        {
            _opacity = value;
            OnPropertyChanged(nameof(Opacity));
        }
    }
    
    // Header is always fully opaque but darker version of the color
    public string HeaderColor => _hexColor.StartsWith("#CC") ? _hexColor.Replace("#CC", "#FF") : 
                                  _hexColor.StartsWith("#") && _hexColor.Length == 7 ? "#FF" + _hexColor.Substring(1) : _hexColor;
    
    // Computed properties for UI
    public bool HasFiles => Files.Count > 0;
    public bool IsEmpty => Files.Count == 0;

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

            FilesChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(IsEmpty));
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

    // Event to request rule update (Id, Extension)
    public event Action<int, string>? RequestRuleUpdate;
    public int Id { get; set; }

    public void AddFiles(string[] files)
    {
        // When dropping files onto this fence:
        // 1. If the file is NOT on the desktop, move it to the desktop.
        // 2. If it IS on the desktop, do nothing (it's already there).
        // 3. Check if extension matches. If not, ask user to add it.

        foreach (var file in files)
        {
            if (!File.Exists(file) && !Directory.Exists(file)) continue;

            try
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(_folderPath, fileName); // _folderPath is Desktop
                var ext = Path.GetExtension(file).ToLower();

                // 1. Check extension match FIRST
                // We do this check regardless of whether it moved or was already there.
                if (_extensions.Length > 0 && !_extensions.Contains(ext) && !string.IsNullOrEmpty(ext))
                {
                    // Prompt user
                    var result = System.Windows.MessageBox.Show(
                        $"El archivo '{fileName}' ({ext}) no pertenece a este fence.\n¿Quieres añadir '{ext}' a las reglas?", 
                        "Actualizar Reglas", 
                        System.Windows.MessageBoxButton.YesNo, 
                        System.Windows.MessageBoxImage.Question);
                        
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        RequestRuleUpdate?.Invoke(Id, ext);
                        // Return/Continue because the window will reload
                        continue; 
                    }
                    else
                    {
                        // User said No, so we skip this file
                        continue;
                    }
                }

                // 2. Check if source and dest are same
                if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Already on desktop, and rules are fine
                }

                // Handle duplicate names
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    // string ext = Path.GetExtension(fileName); // already calc
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

    // Commands
    public System.Windows.Input.ICommand OpenCommand => new RelayCommand(param => 
    {
        if (param is FileItemViewModel file)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file.FullPath) { UseShellExecute = true }); } catch { }
        }
    });

    public System.Windows.Input.ICommand ShowInExplorerCommand => new RelayCommand(param => 
    {
        if (param is FileItemViewModel file)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FullPath}\""); } catch { }
        }
    });

    public System.Windows.Input.ICommand CopyCommand => new RelayCommand(param => 
    {
        if (param is FileItemViewModel file)
        {
            try 
            {
                var list = new System.Collections.Specialized.StringCollection { file.FullPath };
                System.Windows.Clipboard.SetFileDropList(list);
            } 
            catch { }
        }
    });

    public System.Windows.Input.ICommand DeleteCommand => new RelayCommand(param => 
    {
        if (param is FileItemViewModel file)
        {
            var result = System.Windows.MessageBox.Show(
                $"¿Estás seguro de que quieres eliminar '{file.Name}'?\nIrreversiblemente (por ahora).",
                "Eliminar Archivo",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try 
                {
                    System.IO.File.Delete(file.FullPath);
                    // Watcher will remove it from list
                } 
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error al eliminar: {ex.Message}");
                }
            }
        }
    });
}
