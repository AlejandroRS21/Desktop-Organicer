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
    
    // Editable categories
    private Dictionary<string, List<string>> _editableCategories = new();

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
            
            // Initialize editable categories from defaults
            foreach (var cat in FenceManager.GetDefaultCategories())
            {
                _editableCategories[cat.Key] = cat.Value.ToList();
            }
            
            PopulateCategories();
        };
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        
        if (TabPersonalization?.IsChecked == true)
        {
            PersonalizationPanel.Visibility = Visibility.Visible;
            CategoriesPanel.Visibility = Visibility.Collapsed;
        }
        else if (TabCategories?.IsChecked == true)
        {
            PersonalizationPanel.Visibility = Visibility.Collapsed;
            CategoriesPanel.Visibility = Visibility.Visible;
        }
    }

    private void PopulateCategories()
    {
        CategoriesList.Children.Clear();
        
        foreach (var category in _editableCategories)
        {
            var card = CreateEditableCategoryCard(category.Key, category.Value);
            CategoriesList.Children.Add(card);
        }
        
        // Add info about "Otros"
        var infoCard = CreateInfoCard("📁 Otros", 
            "Esta categoría especial muestra automáticamente todos los archivos y carpetas que no coinciden con ninguna extensión definida arriba.");
        CategoriesList.Children.Add(infoCard);
    }

    private Border CreateEditableCategoryCard(string name, List<string> extensions)
    {
        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 15)
        };
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 10,
            Opacity = 0.05,
            ShadowDepth = 2
        };

        var mainStack = new StackPanel();

        // Header with name
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var header = new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"))
        };
        Grid.SetColumn(header, 0);
        headerGrid.Children.Add(header);

        var countBadge = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 3, 10, 3)
        };
        countBadge.Child = new TextBlock
        {
            Text = $"{extensions.Count} ext.",
            FontSize = 11,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"))
        };
        Grid.SetColumn(countBadge, 1);
        headerGrid.Children.Add(countBadge);

        mainStack.Children.Add(headerGrid);

        // Extensions display as editable badges
        var wrapPanel = new WrapPanel { Margin = new Thickness(0, 15, 0, 0) };
        
        foreach (var ext in extensions)
        {
            var badge = CreateExtensionBadge(name, ext, extensions);
            wrapPanel.Children.Add(badge);
        }

        // Add new extension button
        var addButton = new Button
        {
            Content = "+ Añadir",
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 5, 5),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = name
        };
        addButton.Click += AddExtension_Click;
        
        // Apply template for rounded corners
        addButton.Template = CreateButtonTemplate("#DBEAFE", "#BFDBFE");
        wrapPanel.Children.Add(addButton);

        mainStack.Children.Add(wrapPanel);
        card.Child = mainStack;
        return card;
    }

    private Border CreateExtensionBadge(string categoryName, string ext, List<string> extensions)
    {
        var badge = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 4, 4),
            Margin = new Thickness(0, 0, 5, 5)
        };
        
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        
        stack.Children.Add(new TextBlock
        {
            Text = ext,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"))
        });
        
        // Delete button
        var deleteBtn = new Button
        {
            Content = "×",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Tag = new ExtensionDeleteInfo { Category = categoryName, Extension = ext }
        };
        deleteBtn.Click += DeleteExtension_Click;
        stack.Children.Add(deleteBtn);
        
        badge.Child = stack;
        return badge;
    }

    private void AddExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string categoryName)
        {
            var dialog = new AddExtensionDialog();
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ExtensionResult))
            {
                var ext = dialog.ExtensionResult.Trim().ToLower();
                if (!ext.StartsWith("."))
                    ext = "." + ext;
                
                if (_editableCategories.ContainsKey(categoryName) && !_editableCategories[categoryName].Contains(ext))
                {
                    _editableCategories[categoryName].Add(ext);
                    MarkAsChanged();
                    PopulateCategories();
                }
            }
        }
    }

    private void DeleteExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ExtensionDeleteInfo info)
        {
            if (_editableCategories.ContainsKey(info.Category))
            {
                _editableCategories[info.Category].Remove(info.Extension);
                MarkAsChanged();
                PopulateCategories();
            }
        }
    }

    private ControlTemplate CreateButtonTemplate(string normalColor, string hoverColor)
    {
        var template = new ControlTemplate(typeof(Button));
        
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(normalColor)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
        border.Name = "border";
        
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        
        border.AppendChild(contentPresenter);
        template.VisualTree = border;
        
        return template;
    }

    private Border CreateInfoCard(string name, string description)
    {
        var card = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 15)
        };

        var stack = new StackPanel();
        
        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E"))
        });
        
        stack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A16207")),
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        card.Child = stack;
        return card;
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
        
        // Reset categories
        _editableCategories.Clear();
        foreach (var cat in FenceManager.GetDefaultCategories())
        {
            _editableCategories[cat.Key] = cat.Value.ToList();
        }
        PopulateCategories();
        
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
                    _viewModel.SaveCommand.Execute(null);
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

    private class ExtensionDeleteInfo
    {
        public string Category { get; set; } = "";
        public string Extension { get; set; } = "";
    }
}

/// <summary>
/// Simple dialog to add a new extension.
/// </summary>
public class AddExtensionDialog : Window
{
    private TextBox _textBox;
    public string ExtensionResult { get; private set; } = "";

    public AddExtensionDialog()
    {
        Title = "Añadir Extensión";
        Width = 300;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        
        var stack = new StackPanel { Margin = new Thickness(20) };
        
        stack.Children.Add(new TextBlock 
        { 
            Text = "Introduce la extensión (ej: .pdf)", 
            Margin = new Thickness(0, 0, 0, 10) 
        });
        
        _textBox = new TextBox 
        { 
            Padding = new Thickness(8), 
            FontSize = 14 
        };
        stack.Children.Add(_textBox);
        
        var buttonStack = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        
        var cancelBtn = new Button 
        { 
            Content = "Cancelar", 
            Padding = new Thickness(15, 8, 15, 8),
            Margin = new Thickness(0, 0, 10, 0)
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
        buttonStack.Children.Add(cancelBtn);
        
        var okBtn = new Button 
        { 
            Content = "Añadir", 
            Padding = new Thickness(15, 8, 15, 8),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        okBtn.Click += (s, e) => 
        { 
            ExtensionResult = _textBox.Text;
            DialogResult = true; 
            Close(); 
        };
        buttonStack.Children.Add(okBtn);
        
        stack.Children.Add(buttonStack);
        Content = stack;
        
        Loaded += (s, e) => _textBox.Focus();
    }
}
