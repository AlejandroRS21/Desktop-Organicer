using System.Windows;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
