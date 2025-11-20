using System;

namespace DesktopOrganizer.Core.Models;

public class FileLog
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Moved, Copied, Deleted
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
