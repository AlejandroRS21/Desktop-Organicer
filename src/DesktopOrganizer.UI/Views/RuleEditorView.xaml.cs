using System.Windows;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Views;

public partial class RuleEditorView : Window
{
    public RuleEditorView(RuleEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
