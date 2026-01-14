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
        // Use protected field directly
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

                // 2. Check Inclusion (Priority Medium - Force Include)
                bool isIncluded = _includedFiles.Contains(fileName);
                
                // 3. Check "Others" Logic (Not in excluded extensions)
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
            var directories = Directory.GetDirectories(_folderPath);
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

    private void OnFilesChanged()
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
