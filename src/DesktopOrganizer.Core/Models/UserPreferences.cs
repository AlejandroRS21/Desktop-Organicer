namespace DesktopOrganizer.Core.Models;

public class UserPreferences
{
    public int Id { get; set; }
    public string MonitoredDirectories { get; set; } = string.Empty; // Semicolon separated paths
    public bool EnableAutoStart { get; set; }
    public bool EnableNotifications { get; set; }
    
    // Fence Styling
    public double FenceOpacity { get; set; } = 0.6;
    public string FenceColorHex { get; set; } = "#1E293B"; // Dark Slate
    public bool EnableFenceBlur { get; set; } = true;

    // Initialization flag to distinguish first run from "all deleted"
    public bool IsFirstRun { get; set; } = true;
}
