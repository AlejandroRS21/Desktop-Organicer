namespace DesktopOrganizer.Core.Interfaces;

public interface IRule
{
    string Name { get; set; }
    int Priority { get; set; }
    bool IsActive { get; set; }
    string TargetCategory { get; set; }
    
    bool Evaluate(string filePath);
}
