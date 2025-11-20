using System;
using System.Linq;
using System.IO;
using DesktopOrganizer.Core.Interfaces;

namespace DesktopOrganizer.Core.Rules;

public class ExtensionRule : IRule
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public string TargetCategory { get; set; } = string.Empty;
    
    public string[] Extensions { get; set; } = Array.Empty<string>();

    public bool Evaluate(string filePath)
    {
        if (!IsActive || string.IsNullOrEmpty(filePath)) return false;
        
        var fileExtension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(fileExtension)) return false;
        
        return Extensions.Any(ext => ext.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
    }
}
