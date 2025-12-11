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
using DesktopOrganizer.UI.Services;

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

    /// <summary>
    /// Get a service from the DI container. Used for accessing services from code-behind.
    /// </summary>
    public static T? GetService<T>() where T : class
    {
        if (Current is App app && app._serviceProvider != null)
        {
            return app._serviceProvider.GetService<T>();
        }
        return null;
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

        // Factories (Scoped Window Creation)
        services.AddSingleton<Func<RuleEditorView>>(provider => () => 
        {
            var scope = provider.CreateScope();
            var view = scope.ServiceProvider.GetRequiredService<RuleEditorView>();
            view.Closed += (s, e) => scope.Dispose();
            // Trigger refresh on load because we want latest fences
            if (view.DataContext is RuleEditorViewModel vm)
            {
                vm.LoadRulesCommand.Execute(null);
            }
            return view;
        });

        services.AddSingleton<Func<LogsView>>(provider => () => 
        {
            var scope = provider.CreateScope();
            var view = scope.ServiceProvider.GetRequiredService<LogsView>();
            view.Closed += (s, e) => scope.Dispose();
            return view;
        });

        services.AddSingleton<Func<SettingsView>>(provider => () => 
        {
            var scope = provider.CreateScope();
            var view = scope.ServiceProvider.GetRequiredService<SettingsView>();
            view.Closed += (s, e) => scope.Dispose();
            return view;
        });
        
        // Tray Icon (Settings Window Factory)
        services.AddSingleton<TrayIconService>(provider => 
        {
            var fenceManager = provider.GetRequiredService<FenceManager>();
            var fileOrganizer = provider.GetRequiredService<FileOrganizer>(); // Added
            return new TrayIconService(() => 
            {
                var scope = provider.CreateScope();
                var window = scope.ServiceProvider.GetRequiredService<SettingsView>();
                window.Closed += (s, e) => scope.Dispose();
                return window;
            }, fenceManager, fileOrganizer); // Added arg
        });
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
                    // Force a raw SQL query to check for column existence.
                    // This is more reliable than LINQ for schema validation on empty tables.
                    context.Database.ExecuteSqlRaw("SELECT ExcludedFiles FROM Fences LIMIT 1");
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

            // Start Tray Icon
            var trayService = _serviceProvider.GetRequiredService<TrayIconService>();
            
            // Prevent app from closing when main window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // We don't show main window by default anymore, we let the user open it from tray
            // var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            // mainWindow.Show();
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
