using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using DesktopOrganizer.UI.Helpers;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// ViewModel for the "Otros" fence that shows files NOT matching any known extension.
/// </summary>
public class OthersFenceViewModel : FenceViewModel
{
    private readonly HashSet<string> _excludeExtensions;

    public OthersFenceViewModel(string title, string folderPath, HashSet<string> excludeExtensions, IEnumerable<string> includedFiles, IEnumerable<string> excludedFiles) 
        : base(title, folderPath, Array.Empty<string>(), includedFiles, excludedFiles)
    {
        _excludeExtensions = excludeExtensions;
        LoadOtherFiles();
    }

    private void LoadOtherFiles()
    {
        var folderPath = GetFolderPath();
        if (!Directory.Exists(folderPath)) return;

        var files = Directory.GetFiles(folderPath);
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            Files.Clear();
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var ext = Path.GetExtension(file).ToLower();

                // 1. Check Exclusion (Priority High)
                if (_excludedFiles.Contains(fileName)) continue;

                // 2. Check Inclusion (Priority Medium - Force Include)
                bool isIncluded = _includedFiles.Contains(fileName);
                
                // 3. Check "Others" Logic (Not in excluded extensions)
                // Include files that DON'T match any known extension OR are explicitly included
                if (isIncluded || !_excludeExtensions.Contains(ext))
                {
                    Files.Add(new FileItemViewModel
                    {
                        Name = fileName,
                        FullPath = file,
                        Icon = IconHelper.GetIcon(file)
                    });
                }
            }
            
            // Also include directories (folders on desktop)
            var directories = Directory.GetDirectories(folderPath);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                // Skip system/hidden folders
                if (dirName.StartsWith(".")) continue;
                
                Files.Add(new FileItemViewModel
                {
                    Name = dirName,
                    FullPath = dir,
                    Icon = IconHelper.GetIcon(dir)
                });
            }

            OnFilesChanged();
        });
    }

    private string GetFolderPath()
    {
        // Use reflection to get the private _folderPath field
        var field = typeof(FenceViewModel).GetField("_folderPath", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(this) as string ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    private void OnFilesChanged()
    {
        // Trigger the FilesChanged event through base class
        var property = typeof(FenceViewModel).GetProperty("HasFiles");
        if (property != null)
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    protected new void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
    }
}
