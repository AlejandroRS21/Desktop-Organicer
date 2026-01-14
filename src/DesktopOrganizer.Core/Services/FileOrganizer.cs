using System;
using System.IO;
using System.Threading.Tasks;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Core.Services;

public class FileOrganizer
{
    private readonly RuleEngine _ruleEngine;
    // In a real scenario, we would inject a logger or repository here
    
    public FileOrganizer(RuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    public async Task LoadRulesAsync()
    {
        await _ruleEngine.LoadRulesAsync();
    }

    public async Task OrganizeFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var targetCategory = _ruleEngine.EvaluateFile(filePath);
        if (string.IsNullOrEmpty(targetCategory)) return;

        try 
        {
            // Get the Desktop path
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            
            // Create category folder directly on Desktop
            var destinationFolder = Path.Combine(desktopPath, targetCategory);
            
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            var fileName = Path.GetFileName(filePath);
            var destinationPath = Path.Combine(destinationFolder, fileName);

            // Conflict Resolution: Rename if exists
            if (File.Exists(destinationPath))
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                destinationPath = Path.Combine(destinationFolder, $"{fileNameWithoutExt}_{timestamp}{extension}");
            }

            // Move the file
            // Using Task.Run to offload IO to thread pool
            await Task.Run(() => File.Move(filePath, destinationPath));
            
            // TODO: Log success
            // _repository.AddAsync(new FileLog { ... });
        }
        catch (Exception ex)
        {
            // Log error
            // _logger.LogError(ex, "Error organizing file {FilePath}", filePath);
            Console.WriteLine($"Error organizing file {filePath}: {ex.Message}");
        }
    }
    public async Task<System.Collections.Generic.List<SimulationResult>> SimulateOrganizationAsync(string directoryPath)
    {
        var results = new System.Collections.Generic.List<SimulationResult>();
        if (!Directory.Exists(directoryPath)) return results;

        // Ensure rules are loaded
        if (_ruleEngine.Rules == null || !_ruleEngine.Rules.Any())
        {
            await LoadRulesAsync();
        }

        var files = Directory.GetFiles(directoryPath);
        
        await Task.Run(() =>
        {
            foreach (var filePath in files)
            {
                // Skip if hidden or system file
                var attr = File.GetAttributes(filePath);
                if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden || 
                    (attr & FileAttributes.System) == FileAttributes.System)
                    continue;

                var targetCategory = _ruleEngine.EvaluateFile(filePath);
                if (!string.IsNullOrEmpty(targetCategory))
                {
                    var fileName = Path.GetFileName(filePath);
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var destinationFolder = Path.Combine(desktopPath, targetCategory);
                    var destinationPath = Path.Combine(destinationFolder, fileName);
                    
                    results.Add(new SimulationResult
                    {
                        FileName = fileName,
                        OriginalPath = filePath,
                        TargetPath = destinationPath,
                        TargetCategory = targetCategory,
                        MatchedRule = targetCategory, // Using category as rule indicator for now
                        WouldMove = true
                    });
                }
            }
        });

        return results;
    }
}
