using System;
using System.Linq;
using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Serilog;
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
        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing application: {ex.Message}\n\n{ex.StackTrace}", 
                "Initialization Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            throw;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        IConfiguration configuration = builder.Build();

        // Logging
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        services.AddLogging(loggingBuilder =>
            loggingBuilder.AddSerilog(dispose: true));

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
        services.AddScoped<RuleEngine>(); 
        services.AddScoped<FileOrganizer>();
        
        // Register FenceManager
        services.AddSingleton<DesktopOrganizer.UI.Services.FenceManager>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<RuleEditorViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<RuleEditorView>();
        services.AddTransient<LogsView>();
        services.AddTransient<SettingsView>();

        // Factories
        services.AddSingleton<Func<RuleEditorView>>(provider => () => provider.GetRequiredService<RuleEditorView>());
        services.AddSingleton<Func<LogsView>>(provider => () => provider.GetRequiredService<LogsView>());
        services.AddSingleton<Func<SettingsView>>(provider => () => provider.GetRequiredService<SettingsView>());
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            // Ensure database is created with latest schema
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DesktopOrganizerDbContext>();
                
                try
                {
                    _ = context.UserPreferences.Any();
                }
                catch
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
                }
                
                context.Database.EnsureCreated();
                
                if (!context.Rules.Any())
                {
                    var defaultRules = new[]
                    {
                        new DesktopOrganizer.Core.Models.Rule
                        {
                            Name = "Documentos",
                            Priority = 1,
                            IsActive = true,
                            TargetCategory = "Documentos",
                            RuleType = "ExtensionRule",
                            Configuration = "[\".pdf\", \".doc\", \".docx\", \".txt\", \".xlsx\", \".xls\", \".ppt\", \".pptx\"]"
                        },
                        new DesktopOrganizer.Core.Models.Rule
                        {
                            Name = "Imágenes",
                            Priority = 2,
                            IsActive = true,
                            TargetCategory = "Imagenes",
                            RuleType = "ExtensionRule",
                            Configuration = "[\".jpg\", \".jpeg\", \".png\", \".gif\", \".bmp\", \".svg\", \".webp\"]"
                        },
                        new DesktopOrganizer.Core.Models.Rule
                        {
                            Name = "Videos",
                            Priority = 3,
                            IsActive = true,
                            TargetCategory = "Videos",
                            RuleType = "ExtensionRule",
                            Configuration = "[\".mp4\", \".avi\", \".mkv\", \".mov\", \".wmv\", \".flv\"]"
                        },
                        new DesktopOrganizer.Core.Models.Rule
                        {
                            Name = "Música",
                            Priority = 4,
                            IsActive = true,
                            TargetCategory = "Musica",
                            RuleType = "ExtensionRule",
                            Configuration = "[\".mp3\", \".wav\", \".flac\", \".aac\", \".ogg\", \".m4a\"]"
                        },
                        new DesktopOrganizer.Core.Models.Rule
                        {
                            Name = "Aplicaciones",
                            Priority = 5,
                            IsActive = true,
                            TargetCategory = "Aplicaciones",
                            RuleType = "ExtensionRule",
                            Configuration = "[\".exe\", \".msi\", \".dmg\", \".app\"]"
                        },
                        new DesktopOrganizer.Core.Models.Rule
                        {
                            Name = "Comprimidos",
                            Priority = 6,
                            IsActive = true,
                            TargetCategory = "Comprimidos",
                            RuleType = "ExtensionRule",
                            Configuration = "[\".zip\", \".rar\", \".7z\", \".tar\", \".gz\"]"
                        }
                    };
                    
                    context.Rules.AddRange(defaultRules);
                    context.SaveChanges();
                }
            }

            // Initialize Fences
            var fenceManager = _serviceProvider.GetRequiredService<DesktopOrganizer.UI.Services.FenceManager>();
            fenceManager.InitializeFences();

            // Start File Watcher Service (DISABLED for Virtual Fences mode)
            // var fileWatcherService = _serviceProvider.GetRequiredService<IFileWatcher>();
            // fileWatcherService.Start();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error starting application: {ex.Message}\n\n{ex.StackTrace}", 
                "Startup Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
