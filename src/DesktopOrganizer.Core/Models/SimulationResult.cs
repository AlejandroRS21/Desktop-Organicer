namespace DesktopOrganizer.Core.Models;

public class SimulationResult
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string TargetCategory { get; set; } = string.Empty;
    public string MatchedRule { get; set; } = string.Empty;
    public bool WouldMove { get; set; }
}
