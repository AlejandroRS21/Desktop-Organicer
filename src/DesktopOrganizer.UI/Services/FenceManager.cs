using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using DesktopOrganizer.UI.ViewModels;
using DesktopOrganizer.UI.Views;

namespace DesktopOrganizer.UI.Services;

public class FenceManager
{
    private readonly List<FenceWindow> _openFences = new List<FenceWindow>();

    public void InitializeFences()
    {
        // Close existing fences
        foreach (var fence in _openFences.ToList())
        {
            fence.Close();
        }
        _openFences.Clear();

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // Define categories and their extensions
        var categories = new Dictionary<string, string[]>
        {
            { "Documentos", new[] { ".pdf", ".doc", ".docx", ".txt", ".xls", ".xlsx", ".ppt", ".pptx", ".odt" } },
            { "Imagenes", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico" } },
            { "Videos", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" } },
            { "Musica", new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg" } },
            { "Aplicaciones", new[] { ".exe", ".lnk", ".msi" } },
            { "Comprimidos", new[] { ".zip", ".rar", ".7z", ".tar", ".gz" } }
        };

        // Position logic
        double startX = SystemParameters.WorkArea.Width - 300; // Start from right side
        double startY = 50;
        double offset = 320; // Height + margin

        foreach (var category in categories)
        {
            // Create fence watching Desktop but filtering by extensions
            CreateFence(category.Key, desktopPath, category.Value, startX, startY);
            
            startY += offset;
            
            // Wrap to next column if too tall
            if (startY > SystemParameters.WorkArea.Height - 300)
            {
                startY = 50;
                startX -= 270;
            }
        }
    }

    private void CreateFence(string title, string path, string[] extensions, double left, double top)
    {
        var viewModel = new FenceViewModel(title, path, extensions);
        var window = new FenceWindow(viewModel);
        window.Left = left;
        window.Top = top;
        window.Show();
        _openFences.Add(window);
        
        // Remove from list when closed
        window.Closed += (s, e) => _openFences.Remove(window);
    }
}
