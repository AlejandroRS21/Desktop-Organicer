namespace DesktopOrganizer.Core.Models;

public class UserPreferences
{
    public int Id { get; set; }
    public string MonitoredDirectories { get; set; } = string.Empty; // Semicolon separated paths
    public bool EnableAutoStart { get; set; }
    public bool EnableNotifications { get; set; }
}
