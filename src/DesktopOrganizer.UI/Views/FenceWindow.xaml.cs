using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DesktopOrganizer.UI.Helpers;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Views;

public partial class FenceWindow : Window
{
    private readonly FenceViewModel _viewModel;

    public FenceWindow(FenceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Pin this window to the desktop background
        DesktopWindowHelper.SetToDesktop(this);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                _viewModel.AddFiles(files);
            }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void File_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var border = sender as System.Windows.Controls.Grid;
            if (border?.DataContext is FileItemViewModel file)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
