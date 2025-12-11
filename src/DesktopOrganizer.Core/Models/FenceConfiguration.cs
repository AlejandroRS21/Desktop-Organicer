namespace DesktopOrganizer.Core.Models;

public class FenceConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Extensions { get; set; } = string.Empty; // Semicolon separated, or specific keyword like "*"
    public string Category { get; set; } = string.Empty; // "Documentos", "Imagenes", etc. or "Custom"
    
    // Manual overrides
    public string IncludedFiles { get; set; } = string.Empty; // Semicolon separated file names
    public string ExcludedFiles { get; set; } = string.Empty; // Semicolon separated file names
}
