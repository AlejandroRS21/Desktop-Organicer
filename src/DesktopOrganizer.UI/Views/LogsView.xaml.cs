using System.Windows;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Views;

public partial class LogsView : Window
{
    public LogsView(LogsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
