using System.Text.RegularExpressions;
using System.IO;
using DesktopOrganizer.Core.Interfaces;

namespace DesktopOrganizer.Core.Rules;

public class NamePatternRule : IRule
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public string TargetCategory { get; set; } = string.Empty;
    
    public string Pattern { get; set; } = string.Empty;

    public bool Evaluate(string filePath)
    {
        if (!IsActive || string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(Pattern)) return false;
        
        var fileName = Path.GetFileName(filePath);
        try
        {
            return Regex.IsMatch(fileName, Pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            // Invalid regex pattern
            return false;
        }
    }
}
