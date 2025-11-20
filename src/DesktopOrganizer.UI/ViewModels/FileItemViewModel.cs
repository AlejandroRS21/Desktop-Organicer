using System.Windows.Media;

namespace DesktopOrganizer.UI.ViewModels;

public class FileItemViewModel
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public ImageSource Icon { get; set; }
}
