namespace DesktopOrganizer.Core.Models;

public class Rule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string TargetCategory { get; set; } = string.Empty;
    
    // Discriminator for different rule types if stored in single table
    public string RuleType { get; set; } = string.Empty;
    
    // JSON serialized configuration for specific rule logic (e.g. extensions list, regex pattern)
    public string Configuration { get; set; } = string.Empty;
}
