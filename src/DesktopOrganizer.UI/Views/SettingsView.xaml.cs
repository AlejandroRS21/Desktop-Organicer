using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopOrganizer.UI.Services;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Views;

public partial class SettingsView : Window
{
    private readonly SettingsViewModel _viewModel;
    private double _originalOpacity;
    private string _originalColor = "";
    private bool _originalBlur;
    private bool _hasUnsavedChanges = false;
    
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        
        Loaded += (s, e) =>
        {
            _originalOpacity = _viewModel.FenceOpacity;
            _originalColor = _viewModel.FenceColorHex;
            _originalBlur = _viewModel.EnableFenceBlur;
        };
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        
        if (TabPersonalization?.IsChecked == true)
        {
            PersonalizationPanel.Visibility = Visibility.Visible;
            RulesPanel.Visibility = Visibility.Collapsed;
        }
        else if (TabCategories?.IsChecked == true)
        {
            PersonalizationPanel.Visibility = Visibility.Collapsed;
            RulesPanel.Visibility = Visibility.Visible;
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        MarkAsChanged();
        ApplyChangesRealTime();
    }

    private void ColorInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        MarkAsChanged();
        
        var text = ColorInput.Text;
        if (text.StartsWith("#") && (text.Length == 7 || text.Length == 9))
        {
            ApplyChangesRealTime();
        }
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string color)
        {
            _viewModel.FenceColorHex = color;
            MarkAsChanged();
            ApplyChangesRealTime();
        }
    }

    private void MarkAsChanged()
    {
        _hasUnsavedChanges = true;
        UnsavedIndicator.Visibility = Visibility.Visible;
    }

    private void ApplyChangesRealTime()
    {
        try
        {
            var fenceManager = App.GetService<FenceManager>();
            fenceManager?.InitializeFences();
        }
        catch { }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FenceOpacity = _originalOpacity;
        _viewModel.FenceColorHex = _originalColor;
        _viewModel.EnableFenceBlur = _originalBlur;
        
        _hasUnsavedChanges = false;
        UnsavedIndicator.Visibility = Visibility.Collapsed;
        
        ApplyChangesRealTime();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "Tienes cambios sin guardar. ¿Qué deseas hacer?",
                "Cambios sin guardar",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    _viewModel.SavePreferencesCommand.Execute(null);
                    break;
                case MessageBoxResult.No:
                    _viewModel.FenceOpacity = _originalOpacity;
                    _viewModel.FenceColorHex = _originalColor;
                    _viewModel.EnableFenceBlur = _originalBlur;
                    ApplyChangesRealTime();
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
    }
}
