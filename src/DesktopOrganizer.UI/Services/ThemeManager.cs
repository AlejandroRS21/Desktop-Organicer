using System;
using System.Linq;
using System.Windows;

namespace DesktopOrganizer.UI.Services;

public enum AppTheme
{
    Light,
    Dark
}

public class ThemeManager
{
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var merged = app.Resources.MergedDictionaries;
        
        // Find existing theme dictionary (assuming it contains "Themes/" in source)
        // Note: Source URI might be relative or pack URI. 
        // In App.xaml we used "Resources/Themes/Light.xaml".
        
        var currentThemeDict = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
        
        var newThemeSource = theme == AppTheme.Dark 
            ? "Resources/Themes/Dark.xaml" 
            : "Resources/Themes/Light.xaml";
            
        var uri = new Uri(newThemeSource, UriKind.Relative);
        
        if (currentThemeDict != null)
        {
            if (currentThemeDict.Source.OriginalString == newThemeSource) return; // Already set
            
            merged.Remove(currentThemeDict);
        }

        var newDict = new ResourceDictionary { Source = uri };
        merged.Add(newDict);
        
        CurrentTheme = theme;
    }
}
