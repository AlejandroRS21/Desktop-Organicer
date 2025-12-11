using System.Windows.Media;

namespace DesktopOrganizer.UI.ViewModels;

public class FileItemViewModel
{
    public required string Name { get; set; }
    public required string FullPath { get; set; }
    public required System.Windows.Media.ImageSource Icon { get; set; }
}
