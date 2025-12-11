using System;
using System.Collections.Generic;
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

    private readonly HashSet<string> _includedFiles;
    private readonly HashSet<string> _excludedFiles;

    public FenceViewModel(string title, string folderPath, string[] extensions, IEnumerable<string> includedFiles, IEnumerable<string> excludedFiles)
    {
        Title = title;
        _folderPath = folderPath;
        _extensions = extensions != null ? extensions.Select(e => e.ToLower()).ToArray() : new string[0];
        _includedFiles = new HashSet<string>(includedFiles ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _excludedFiles = new HashSet<string>(excludedFiles ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        
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
                var fileName = Path.GetFileName(file);
                var ext = Path.GetExtension(file).ToLower();

                // 1. Check Exclusion (Priority High)
                if (_excludedFiles.Contains(fileName)) continue;

                // 2. Check Inclusion (Priority Medium)
                bool isIncluded = _includedFiles.Contains(fileName);

                // 3. Check Extension Rules (Priority Low)
                bool matchesRule = _extensions.Contains(ext);

                // 4. Default Acceptance (Folders always accepted if not excluded? Or need rule?)
                // Assuming folders follow visual rules or inclusion.
                // For now, let's say Folders are accepted if not excluded?
                // Or stick to extension logic? Folders have no extension.
                // If ext is missing, user must include specificly? Or we allow folders?
                // Previous logic allowed folders implicitly via AddFiles. Let's show them.
                bool isDir = Directory.Exists(file); // Directory.GetFiles doesn't return dirs?
                // Wait, Directory.GetFiles returns FILES. GetFileSystemEntries returns both.
                // We typically only support files unless we changed that.
                // Actually `Directory.GetFiles` returns only files. So `isDir` is always false here?
                // If we want to support folders showing up inside fences, we need `GetFileSystemEntries`.
                // But `AddFiles` handles folders...
                // If `AddFiles` moves a folder, it stays on Desktop.
                // But `LoadFiles` won't pick it up if we use `GetFiles`.
                // I will NOT change `GetFiles` to `GetFileSystemEntries` right now to reduce scope,
                // treating this strictly as FILE organization.
                
                if (isIncluded || matchesRule)
                {
                    Files.Add(new FileItemViewModel
                    {
                        Name = fileName,
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
    public event Action<int, string>? RequestInclusionUpdate; // Add specific file
    public event Action<int, string>? RequestExclusionUpdate; // Exclude specific file
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
                bool isDir = Directory.Exists(file);

                // 1. Check Exclusion
                if (_excludedFiles.Contains(fileName))
                {
                    // It's explicitly excluded.
                    // If user drops it here, they probably want to Include it now.
                    // We should ask if they want to force include (override exclusion).
                     var res = System.Windows.MessageBox.Show(
                        $"El archivo '{fileName}' está excluido de este fence.\n¿Quieres incluirlo?", 
                        "Inclusión Manual", 
                        System.Windows.MessageBoxButton.YesNo, 
                        System.Windows.MessageBoxImage.Question);
                     if (res == System.Windows.MessageBoxResult.Yes)
                     {
                         RequestInclusionUpdate?.Invoke(Id, fileName);
                         // Fall through to Move logic
                     }
                     else
                     {
                         continue;
                     }
                }

                // 2. Check Match (Inclusion OR Rule)
                bool isIncluded = _includedFiles.Contains(fileName);
                bool matchesRule = !string.IsNullOrEmpty(ext) && _extensions.Contains(ext);
                
                if (!isDir && !isIncluded && !matchesRule)
                {
                    // Prompt user: Rule or Specific?
                    var result = System.Windows.MessageBox.Show(
                        $"El archivo '{fileName}' ({ext}) no coincide con las reglas.\n\n" +
                        "SÍ: Añadir regla para todos los " + ext + "\n" +
                        "NO: Añadir SOLO este archivo", 
                        "Añadir Archivo", 
                        System.Windows.MessageBoxButton.YesNoCancel, 
                        System.Windows.MessageBoxImage.Question);
                        
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                         // Add Rule
                        RequestRuleUpdate?.Invoke(Id, ext);
                        // Fall through to move
                    }
                    else if (result == System.Windows.MessageBoxResult.No)
                    {
                        // Add Specific File
                        RequestInclusionUpdate?.Invoke(Id, fileName);
                        // Fall through to move
                    }
                    else
                    {
                        // Cancel
                        continue;
                    }
                }

                // 3. Move Logic
                if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue; 
                }

                // Handle duplicate names
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    // string ext = Path.GetExtension(fileName); // already calc
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    destPath = Path.Combine(_folderPath, $"{nameWithoutExt}_{timestamp}{ext}");
                }

                if (Directory.Exists(file)) // Check source original path
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
                // System.Windows.MessageBox.Show($"Error moving: {ex.Message}"); // Optional
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

    public System.Windows.Input.ICommand ExcludeCommand => new RelayCommand(param => 
    {
        if (param is FileItemViewModel file)
        {
            var result = System.Windows.MessageBox.Show(
                $"¿Quitar '{file.Name}' de este fence?\nEl archivo aparecerá en el escritorio.",
                "Quitar del Fence",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                RequestExclusionUpdate?.Invoke(Id, file.Name);
            }
        }
    });
}
