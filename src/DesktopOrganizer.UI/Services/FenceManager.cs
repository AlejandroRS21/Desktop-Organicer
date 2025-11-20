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

        // Define known categories
        var knownCategories = new[] { "Documentos", "Imagenes", "Videos", "Musica", "Aplicaciones", "Comprimidos" };

        // Position logic
        double startX = SystemParameters.WorkArea.Width - 300; // Start from right side
        double startY = 50;
        double offset = 320; // Height + margin

        foreach (var category in knownCategories)
        {
            var dirPath = Path.Combine(desktopPath, category);
            
            // Ensure directory exists so the fence can be created
            if (!Directory.Exists(dirPath))
            {
                try 
                {
                    Directory.CreateDirectory(dirPath);
                }
                catch { continue; } // Skip if permission denied
            }

            CreateFence(category, dirPath, startX, startY);
            
            startY += offset;
            
            // Wrap to next column if too tall
            if (startY > SystemParameters.WorkArea.Height - 300)
            {
                startY = 50;
                startX -= 270;
            }
        }
    }

    private void CreateFence(string title, string path, double left, double top)
    {
        var vm = new FenceViewModel(title, path);
        var window = new FenceWindow(vm)
        {
            Left = left,
            Top = top
        };
        
        window.Show();
        _openFences.Add(window);
        
        // Remove from list when closed
        window.Closed += (s, e) => _openFences.Remove(window);
    }
}
