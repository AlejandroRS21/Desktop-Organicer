using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopOrganizer.UI.Helpers;

namespace DesktopOrganizer.UI.ViewModels;

public partial class FenceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    // Use protected set if needed, or keep as is. But ObservableObject doesn't support readonly fields easily with binding? 
    // Actually these are not observable properties in the original code, just readonly fields.
    protected string _folderPath; // Changed to protected to access in derived class without reflection
    private readonly string[] _extensions; 
    private FileSystemWatcher _watcher = null!;

    public ObservableCollection<FileItemViewModel> Files { get; } = new ObservableCollection<FileItemViewModel>();

    public event EventHandler? FilesChanged;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderColor))]
    private string _hexColor = "#CC1E293B";

    [ObservableProperty]
    private double _opacity = 0.85;
    
    public string HeaderColor => HexColor.StartsWith("#CC") ? HexColor.Replace("#CC", "#FF") : 
                                  HexColor.StartsWith("#") && HexColor.Length == 7 ? "#FF" + HexColor.Substring(1) : HexColor;
    
    public bool HasFiles => Files.Count > 0;
    public bool IsEmpty => Files.Count == 0;

    protected readonly HashSet<string> _includedFiles;
    protected readonly HashSet<string> _excludedFiles;

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

                if (_excludedFiles.Contains(fileName)) continue;

                bool isIncluded = _includedFiles.Contains(fileName);
                bool matchesRule = _extensions.Contains(ext);
                
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
        _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite; 
        _watcher.Created += (s, e) => RefreshFiles();
        _watcher.Deleted += (s, e) => RefreshFiles();
        _watcher.Renamed += (s, e) => RefreshFiles();
        _watcher.EnableRaisingEvents = true;
    }

    private void RefreshFiles()
    {
        Application.Current.Dispatcher.Invoke(() => LoadFiles());
    }

    public event Action<int, string>? RequestRuleUpdate;
    public event Action<int, string>? RequestInclusionUpdate;
    public event Action<int, string>? RequestExclusionUpdate;
    public int Id { get; set; }

    public void AddFiles(string[] files)
    {
        foreach (var file in files)
        {
            if (!File.Exists(file) && !Directory.Exists(file)) continue;

            try
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(_folderPath, fileName);
                var ext = Path.GetExtension(file).ToLower();
                bool isDir = Directory.Exists(file);

                if (_excludedFiles.Contains(fileName))
                {
                     var res = System.Windows.MessageBox.Show(
                        $"El archivo '{fileName}' está excluido de este fence.\n¿Quieres incluirlo?", 
                        "Inclusión Manual", 
                        System.Windows.MessageBoxButton.YesNo, 
                        System.Windows.MessageBoxImage.Question);
                     if (res == System.Windows.MessageBoxResult.Yes)
                     {
                         RequestInclusionUpdate?.Invoke(Id, fileName);
                     }
                     else
                     {
                         continue;
                     }
                }

                bool isIncluded = _includedFiles.Contains(fileName);
                bool matchesRule = !string.IsNullOrEmpty(ext) && _extensions.Contains(ext);
                
                if (!isDir && !isIncluded && !matchesRule)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"El archivo '{fileName}' ({ext}) no coincide con las reglas.\n\n" +
                        "SÍ: Añadir regla para todos los " + ext + "\n" +
                        "NO: Añadir SOLO este archivo", 
                        "Añadir Archivo", 
                        System.Windows.MessageBoxButton.YesNoCancel, 
                        System.Windows.MessageBoxImage.Question);
                        
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        RequestRuleUpdate?.Invoke(Id, ext);
                    }
                    else if (result == System.Windows.MessageBoxResult.No)
                    {
                        RequestInclusionUpdate?.Invoke(Id, fileName);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue; 
                }

                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
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
    }

    [RelayCommand]
    private void Open(FileItemViewModel? file)
    {
        if (file != null)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file.FullPath) { UseShellExecute = true }); } catch { }
        }
    }

    [RelayCommand]
    private void ShowInExplorer(FileItemViewModel? file)
    {
        if (file != null)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FullPath}\""); } catch { }
        }
    }

    [RelayCommand]
    private void Copy(FileItemViewModel? file)
    {
        if (file != null)
        {
            try 
            {
                var list = new System.Collections.Specialized.StringCollection { file.FullPath };
                System.Windows.Clipboard.SetFileDropList(list);
            } 
            catch { }
        }
    }

    [RelayCommand]
    private void Delete(FileItemViewModel? file)
    {
        if (file != null)
        {
            var result = System.Windows.MessageBox.Show(
                $"¿Estás seguro de que quieres eliminar '{file.Name}'?\nSe enviará a la Papelera de Reciclaje.",
                "Eliminar Archivo",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try 
                {
                    ShellHelper.DeleteToRecycleBin(file.FullPath);
                } 
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error al eliminar: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    private void Exclude(FileItemViewModel? file)
    {
        if (file != null)
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
    }

    [RelayCommand]
    private void OpenWith(FileItemViewModel? file)
    {
        if (file != null) ShellHelper.OpenWith(file.FullPath);
    }

    [RelayCommand]
    private void Cut(FileItemViewModel? file)
    {
        if (file != null)
        {
             var list = new System.Collections.Specialized.StringCollection { file.FullPath };
             Clipboard.SetFileDropList(list);
        }
    }

    [RelayCommand]
    private void Properties(FileItemViewModel? file)
    {
        if (file != null) ShellHelper.ShowProperties(file.FullPath);
    }
}
