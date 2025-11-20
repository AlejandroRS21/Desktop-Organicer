using System;
using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.Data.Context;
using DesktopOrganizer.Data.Repositories;
using DesktopOrganizer.Integration.FileSystemWatcher;
using DesktopOrganizer.UI.ViewModels;
using DesktopOrganizer.UI.Views;

namespace DesktopOrganizer.UI;

public partial class App : Application
{
    private IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopOrganizer",
            "database.db");
            
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<DesktopOrganizerDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

        // Services
        services.AddSingleton<IFileWatcher, FileWatcherService>();
        services.AddScoped<RuleEngine>(); // Needs to be configured with rules
        services.AddScoped<FileOrganizer>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Ensure database is created
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DesktopOrganizerDbContext>();
            context.Database.EnsureCreated();
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
