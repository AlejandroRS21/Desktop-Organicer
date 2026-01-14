using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.ViewModels;
using DesktopOrganizer.UI.Views;
using DesktopOrganizer.UI.Helpers;

namespace DesktopOrganizer.UI.Services;

public class FenceManager : IDisposable
{
    public event Action? FencesUpdated;

    private readonly List<FenceWindow> _openFences = new List<FenceWindow>();
    private readonly DesktopIconManager _iconManager = new DesktopIconManager();
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _fencesVisible = true;

    public FenceManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    private void WriteLog(string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "fences_debug.txt");
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: {message}\n");
        }
        catch {}
    }

    public void InitializeFences()
    {
        try
        {
            // NEW APPROACH: Hide the entire native desktop listview window
            _iconManager.HideDesktopListView();
            
            // Close existing fences
            foreach (var fence in _openFences.ToList())
            {
                try { fence.Close(); } catch {}
            }
            _openFences.Clear();

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // Fetch Preferences and Fences from DB
            UserPreferences prefs;
            List<FenceConfiguration> savedFences;
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                
                // Ensure database is created (just in case)
                db.Database.EnsureCreated();
                
                try 
                {
                    var prefsRepo = scope.ServiceProvider.GetRequiredService<Core.Interfaces.IRepository<UserPreferences>>();
                    prefs = prefsRepo.GetAllAsync().Result.FirstOrDefault() ?? new UserPreferences();
                    
                    savedFences = db.Fences.ToList();
                    WriteLog($"DB Loaded. Prefs.IsFirstRun: {prefs.IsFirstRun}. Fences Count: {savedFences.Count}.");
                }
                catch (Exception ex) when (ex.InnerException?.Message.Contains("no such column") == true || ex.Message.Contains("no such table"))
                {
                    WriteLog($"SCHEMA ERROR: {ex.Message}. Resetting DB.");
                    // Schema mismatch detected (missing column IsFirstRun or missing table). Reset DB.
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();
                    
                    // Re-fetch preferences after reset (they will be default)
                    prefs = new UserPreferences();
                    savedFences = new List<FenceConfiguration>();
                }
            }

            // DIAGNOSTIC
            // MessageBox.Show($"Debug: Cargados {savedFences.Count} fences de la BD.");

            // IMPROVED LOGIC: 
            // Only create defaults if IsFirstRun is true AND we really have no fences.
            // If we have fences (even if IsFirstRun is true due to save failure), we assume initialized.
            // If IsFirstRun is True AND Count is 0, it might be first run OR user deleted all. 
            // To be safe, we rely on properly saving IsFirstRun = false.
            
            bool shouldCreateDefaults = prefs.IsFirstRun && savedFences.Count == 0;
            
            // CLEANUP: Force remove any 'Default' or 'Others' category fences from previous runs
            var defaultFences = savedFences.Where(f => f.Category == "Default" || f.Category == "Others").ToList();
            if (defaultFences.Count > 0)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    foreach(var df in defaultFences)
                    {
                        var entity = db.Fences.Find(df.Id);
                        if(entity != null) db.Fences.Remove(entity);
                    }
                    db.SaveChanges();
                }
                // Remove from local list too
                savedFences.RemoveAll(f => f.Category == "Default");
            }

            // Safety: If fences exist but flag is true, fix the flag immediately
            if (prefs.IsFirstRun && savedFences.Count > 0)
            {
                 using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    var dbPrefs = db.UserPreferences.FirstOrDefault();
                    if (dbPrefs != null)
                    {
                        dbPrefs.IsFirstRun = false;
                        db.SaveChanges();
                    }
                    prefs.IsFirstRun = false; 
                }
            }

            if (shouldCreateDefaults)
            {
                // First run ever: Create defaults
                CreateDefaultFences(desktopPath, prefs);
                
                // Update flag - Using DbContext directly to ensure SaveChanges
                prefs.IsFirstRun = false;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    // Fetch fresh entity to update
                    var dbPrefs = db.UserPreferences.FirstOrDefault(); // Helper would be nice but this is reliable
                    if (dbPrefs == null)
                    {
                        dbPrefs = new UserPreferences { IsFirstRun = false };
                        db.UserPreferences.Add(dbPrefs);
                    }
                    else
                    {
                        dbPrefs.IsFirstRun = false;
                    }
                    db.SaveChanges();
                }
            }
            else
            {
                // Load existing (or empty if user deleted all)
                LoadFencesFromConfig(savedFences, desktopPath, prefs);
            }

            _fencesVisible = true;
            
            // Hide all icons that are now shown in fences (after a small delay to let files load)
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Threading.Thread.Sleep(500);
                UpdateHiddenIcons();
                // Notify UI that fences are ready
                Application.Current.Dispatcher.Invoke(() => FencesUpdated?.Invoke());
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error en InitializeFences: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void CreateDefaultFences(string desktopPath, UserPreferences prefs)
    {
        // NO DEFAULT FENCES.
        // User starts with a clean slate.
    }

    private void LoadFencesFromConfig(List<FenceConfiguration> configs, string desktopPath, UserPreferences prefs)
    {
        foreach (var config in configs)
        {
            // Validate position
            EnsureFenceOnScreen(config);
            
            if (config.Category == "Others")
            {
                CreateOthersFence(config, desktopPath, prefs);
            }
            else
            {
                var extensions = config.Extensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                CreateFenceFromConfig(config, desktopPath, prefs, extensions);
            }
        }
    }

    private void EnsureFenceOnScreen(FenceConfiguration config)
    {
        // SystemParameters has individual properties for Virtual Screen bounds
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;
        

        bool isOffScreen = 
            config.Left > vLeft + vWidth - 50 || // Too far right (Right edge of screen)
            config.Left + config.Width < vLeft + 50 || // Too far left
            config.Top > vTop + vHeight - 50 || // Too far down
            config.Top + config.Height < vTop + 50; // Too far up
            
        if (isOffScreen)
        {
            // Reset to top-left or a safe default
            config.Left = 100;
            config.Top = 100;
            config.Width = Math.Max(config.Width, 200);
            config.Height = Math.Max(config.Height, 260);
            
            // Update DB with reset position
            UpdateFenceInDb(config);
        }
    }

    private void CreateOthersFence(FenceConfiguration config, string path, UserPreferences prefs)
    {
        // Re-calculate all known extensions from other fences to exclude
        // This is a bit expensive but ensures consistency
        var allExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultCats = GetDefaultCategories();
        foreach(var ec in defaultCats.Values) 
            foreach(var e in ec) allExtensions.Add(e);

        var includedFiles = (config.IncludedFiles ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        var excludedFiles = (config.ExcludedFiles ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
            
        var viewModel = new OthersFenceViewModel(config.Name, path, allExtensions, includedFiles, excludedFiles);
        viewModel.HexColor = prefs.FenceColorHex;
        viewModel.Opacity = prefs.FenceOpacity;
        
        ConfigureFenceWindow(viewModel, config);
    }

    private void UpdateHiddenIcons()
    {
        // Collect all file names that should be hidden (those shown in any fence)
        var filesToHide = new List<string>();
        
        foreach (var fence in _openFences)
        {
            if (fence.DataContext is FenceViewModel vm)
            {
                foreach (var file in vm.Files)
                {
                    // Get just the file name without extension for matching
                    var fileName = Path.GetFileNameWithoutExtension(file.FullPath);
                    filesToHide.Add(fileName);
                    
                    // Also add with extension
                    filesToHide.Add(Path.GetFileName(file.FullPath));
                }
            }
        }

        if (filesToHide.Count > 0)
        {
            _iconManager.HideIcons(filesToHide);
            // Strict enforcement: Refresh desktop to ensure visual update
            _iconManager.RefreshDesktop();
        }
    }

    private void CreateFenceFromConfig(FenceConfiguration config, string path, UserPreferences prefs, string[] extensions)
    {
        var included = (config.IncludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        var excluded = (config.ExcludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

        var viewModel = new FenceViewModel(config.Name, path, extensions, included, excluded);
        viewModel.HexColor = prefs.FenceColorHex;
        viewModel.Opacity = prefs.FenceOpacity;
        
        ConfigureFenceWindow(viewModel, config);
    }

    private void ConfigureFenceWindow(FenceViewModel viewModel, FenceConfiguration config)
    {
        // Set ID for updates
        viewModel.Id = config.Id;

        viewModel.FilesChanged += (s, e) =>
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(UpdateHiddenIcons), 
                System.Windows.Threading.DispatcherPriority.Background);
        };
        
        viewModel.RequestRuleUpdate += (id, ext) =>
        {
            try 
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    var fence = db.Fences.Find(id);
                    if (fence != null)
                    {
                        string newExt = ext.ToLower();
                        
                        // 1. Remove this extension from ANY other fence (Exclusive Rules)
                        var allFences = db.Fences.ToList(); // Fetch all to check
                        foreach (var otherFence in allFences)
                        {
                            if (otherFence.Id == id) continue; // Skip current
                            
                            var extStr = otherFence.Extensions ?? string.Empty;
                            var otherExts = extStr.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                            
                            if (otherExts.Any(e => e.Equals(newExt, StringComparison.OrdinalIgnoreCase)))
                            {
                                // Remove it
                                var updatedExts = otherExts.Where(e => !e.Equals(newExt, StringComparison.OrdinalIgnoreCase));
                                otherFence.Extensions = string.Join(";", updatedExts);
                                
                                // Update DB (Tracked automatically)
                                
                                // Update UI for other fence
                                // We need to do this on Dispatcher, preventing cross-thread issues if multiple
                                // We'll collect IDs to update
                                int otherId = otherFence.Id;
                                string otherExtStr = otherFence.Extensions;
                                Application.Current.Dispatcher.Invoke(() => 
                                {
                                    UpdateFenceRules(otherId, otherExtStr);
                                });
                            }
                        }

                        // 2. Add to current fence
                        // Check if extension exists (should check case insensitive split)
                        var currentExtStr = fence.Extensions ?? string.Empty;
                        var existing = currentExtStr.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLower());
                        if (!existing.Contains(newExt))
                        {
                            fence.Extensions = (string.IsNullOrEmpty(currentExtStr) ? "" : currentExtStr + ";") + ext;
                            // db.SaveChanges(); // Will be saved at end of scope? 
                            // We need to save changes for both the other fences and this one.
                            db.SaveChanges(); 
                            
                            // Reload fence on UI thread
                            Application.Current.Dispatcher.Invoke(() => 
                            {
                                UpdateFenceRules(id, fence.Extensions);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error actualizando regla: {ex.Message}");
            }
        };

        viewModel.RequestInclusionUpdate += (id, fileName) =>
        {
            try
            {
                 using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    var fence = db.Fences.Find(id);
                    if (fence != null)
                    {
                        string targetFile = fileName.Trim();
                        string ext = Path.GetExtension(targetFile).ToLower();

                        // 1. Add to Current Fence Included List
                        var currentInc = (fence.IncludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        if (!currentInc.Contains(targetFile, StringComparer.OrdinalIgnoreCase))
                        {
                            currentInc.Add(targetFile);
                            fence.IncludedFiles = string.Join(";", currentInc);
                        }
                        
                        // Remove from Excluded if present
                        var currentExc = (fence.ExcludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        if (currentExc.RemoveAll(x => x.Equals(targetFile, StringComparison.OrdinalIgnoreCase)) > 0)
                        {
                             fence.ExcludedFiles = string.Join(";", currentExc);
                        }

                        // 2. Handle Other Fences (Steal / Exclude)
                        var changedFenceIds = new List<int>();
                        var allFences = db.Fences.ToList();
                        
                        foreach (var other in allFences)
                        {
                            if (other.Id == id) continue;

                            bool changed = false;

                            // A. Steal from Included
                            var otherInc = (other.IncludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                            if (otherInc.RemoveAll(x => x.Equals(targetFile, StringComparison.OrdinalIgnoreCase)) > 0)
                            {
                                other.IncludedFiles = string.Join(";", otherInc);
                                changed = true;
                            }

                            // B. Add to Excluded IF rule matches
                            // Only necessary if the other fence has a matching Rule (*.ext)
                            var otherRules = (other.Extensions ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLower());
                            if (otherRules.Contains(ext))
                            {
                                var otherExc = (other.ExcludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                                if (!otherExc.Contains(targetFile, StringComparer.OrdinalIgnoreCase))
                                {
                                    otherExc.Add(targetFile);
                                    other.ExcludedFiles = string.Join(";", otherExc);
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                changedFenceIds.Add(other.Id);
                            }
                        }

                        db.SaveChanges();
                        
                        // Refrescar UI del fence actual
                         Application.Current.Dispatcher.Invoke(() => UpdateFenceRules(id, fence.Extensions));

                        // Refrescar UI de otros fences afectados
                        foreach (var oid in changedFenceIds)
                        {
                            var oFence = allFences.First(f => f.Id == oid);
                            Application.Current.Dispatcher.Invoke(() => UpdateFenceRules(oid, oFence.Extensions));
                        }
                        
                        // Refresh Current UI
                        Application.Current.Dispatcher.Invoke(() => UpdateFenceRules(id, fence.Extensions));
                    }
                }
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error actualizando inclusión: {ex.Message}");
            }
        };

        viewModel.RequestExclusionUpdate += (id, fileName) =>
        {
            try
            {
                 using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    var fence = db.Fences.Find(id);
                    if (fence != null)
                    {
                        string targetFile = fileName.Trim();
                        
                        // 1. Remove from Included (Clean up)
                        var currentInc = (fence.IncludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        if (currentInc.RemoveAll(x => x.Equals(targetFile, StringComparison.OrdinalIgnoreCase)) > 0)
                        {
                            fence.IncludedFiles = string.Join(";", currentInc);
                        }

                        // 2. Add to Excluded 
                        var currentExc = (fence.ExcludedFiles ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        if (!currentExc.Contains(targetFile, StringComparer.OrdinalIgnoreCase))
                        {
                            currentExc.Add(targetFile);
                            fence.ExcludedFiles = string.Join(";", currentExc);
                        }

                        db.SaveChanges();
                        
                        // Refresh Current UI
                        Application.Current.Dispatcher.Invoke(() => UpdateFenceRules(id, fence.Extensions));
                    }
                }
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error excluyendo archivo: {ex.Message}");
            }
        };

        var window = new FenceWindow(viewModel);
        window.Left = config.Left;
        window.Top = config.Top;
        window.Width = config.Width;
        window.Height = config.Height;
        
        // Store Config ID in Tag or similar if needed, or lookup by Name
        window.Tag = config.Id; 
        
        window.Show();
        _openFences.Add(window);
        
        // Debounce simple
        System.Timers.Timer saveTimer = new System.Timers.Timer(500);
        saveTimer.AutoReset = false;
        saveTimer.Elapsed += (s, ev) => 
        {
             Application.Current.Dispatcher.Invoke(() => SaveFenceState(window, config));
        };
        
        window.LocationChanged += (s, e) => {
            saveTimer.Stop(); 
            saveTimer.Start();
        };
        
        window.SizeChanged += (s, e) => {
            saveTimer.Stop();
            saveTimer.Start();
        };
        
        window.Closed += (s, e) =>
        {
            saveTimer.Dispose();
            _openFences.Remove(window);
            if (window.DataContext is FenceViewModel closedVm)
            {
                var filesToShow = closedVm.Files.Select(f => Path.GetFileName(f.FullPath)).ToList();
                _iconManager.ShowIcons(filesToShow);
            }
        };
    }

    /// <summary>
    /// Toggle visibility of all fences. When hidden, restores desktop icons to original positions.
    /// </summary>
    public void ToggleFencesVisibility()
    {
        if (_fencesVisible)
        {
            // Hide fences and show desktop icons
            foreach (var fence in _openFences)
            {
                fence.Visibility = Visibility.Hidden;
            }
            
            // Restore all hidden icons
            _iconManager.ShowAllIcons();
            _fencesVisible = false;
        }
        else
        {
            // Show fences and hide desktop icons
            foreach (var fence in _openFences)
            {
                fence.Visibility = Visibility.Visible;
            }
            
            // Hide icons again
            UpdateHiddenIcons();
            _fencesVisible = true;
        }
    }

    /// <summary>
    /// Close all fences and restore all desktop icons.
    /// </summary>
    public void CloseAllFences()
    {
        // Show all hidden icons first
        _iconManager.ShowAllIcons();
        
        // Then close all fences
        foreach (var fence in _openFences.ToList())
        {
            try { fence.Close(); } catch {}
        }
        _openFences.Clear();
        _fencesVisible = false;
    }

    public bool AreFencesVisible => _fencesVisible && _openFences.Any();
    
    public int FenceCount => _openFences.Count;
    
    public int HiddenIconCount => _iconManager.HiddenIconCount;

    private void CreateFenceFromRect(Rect bounds)
    {
        // 1. Identify icons in this area
        var iconsInArea = _iconManager.GetIconsInRect(bounds);
        
        // 2. Ask user for Fence Name (Optional, could be just "Fence 1")
        // For now, auto-generate unique name
        string name = GetUniqueFenceName("Nuevo Fence");
        
        // 3. Create Fence
        CreateCustomFence(bounds.Left, bounds.Top, bounds.Width, bounds.Height, name, iconsInArea);
    }

    public void CreateCustomFence(double left, double top, double width, double height, string? name = null, List<string>? initialIcons = null)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        
        UserPreferences prefs;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<Core.Interfaces.IRepository<UserPreferences>>();
            prefs = repo.GetAllAsync().Result.FirstOrDefault() ?? new UserPreferences();
        }

        var fenceName = name ?? GetUniqueFenceName("Nuevo Fence");
        
        string includedFiles = initialIcons != null && initialIcons.Count > 0 
            ? string.Join(";", initialIcons) 
            : "";

        var config = new FenceConfiguration
        {
            Name = fenceName,
            Left = left,
            Top = top,
            Width = Math.Max(width, 150),
            Height = Math.Max(height, 100),
            Extensions = "",
            Category = "Custom",
            IncludedFiles = includedFiles
        };
        
        SaveFenceToDb(config);
        CreateFenceFromConfig(config, desktopPath, prefs, Array.Empty<string>());
        FencesUpdated?.Invoke();
        
        // Force immediate update of hidden icons
        Application.Current.Dispatcher.BeginInvoke(new Action(() => 
        {
             UpdateHiddenIcons();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
    
    public void Dispose()
    {
    }

    /// <summary>
    /// Returns a unique name by appending a number if the name already exists.
    /// </summary>
    private string GetUniqueFenceName(string baseName)
    {
        var existingNames = _openFences
            .Select(f => (f.DataContext as FenceViewModel)?.Title)
            .Where(t => t != null)
            .ToHashSet();

        if (!existingNames.Contains(baseName))
            return baseName;

        int counter = 2;
        while (existingNames.Contains($"{baseName} {counter}"))
        {
            counter++;
        }
        
        return $"{baseName} {counter}";
    }

    /// <summary>
    /// Get all open fence titles/categories.
    /// </summary>
    public IEnumerable<string> GetFenceTitles()
    {
        return _openFences
            .Select(f => (f.DataContext as FenceViewModel)?.Title ?? "Unknown")
            .ToList();
    }

    /// <summary>
    /// Save new fence to DB
    /// </summary>
    private void SaveFenceToDb(FenceConfiguration config)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
            db.Fences.Add(config);
            db.SaveChanges();
            // ID is now populated
        }
    }

    /// <summary>
    /// Update existing fence in DB
    /// </summary>
    private void UpdateFenceInDb(FenceConfiguration config)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
            db.Fences.Update(config);
            db.SaveChanges();
        }
    }
    
    /// <summary>
    /// Save fence state (pos/size) from Window
    /// </summary>
    private void SaveFenceState(Window window, FenceConfiguration config)
    {
        // Don't save if it's minimized/collapsed state affecting height uniquely without being true resize?
        // But window.Height changes when we collapse.
        // If we implemented collapse logic, we should probably check if we are in collapsed state before saving Height.
        // Since `FenceWindow` handles collapse internally but updates ActualHeight...
        // For now, let's just save. If user restarts app, it will start with that height.
        // Ideally we should track "ExpandedHeight" in DB if we want to restore specifically that, or just restore state.
        
        if (window.WindowState == WindowState.Minimized) return;

        config.Left = window.Left;
        config.Top = window.Top;
        config.Width = window.Width;
        config.Height = window.Height;
        
        UpdateFenceInDb(config);
    }
    
    public void DeleteFence(FenceViewModel vm)
    {
        WriteLog($"Attemping to delete fence: {vm.Title}");
        var window = _openFences.FirstOrDefault(w => w.DataContext == vm);
        if (window != null)
        {
            int? id = window.Tag as int?;
            WriteLog($"Found window. Tag ID: {id}");
            
            if (id.HasValue)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                    var entity = db.Fences.Find(id.Value);
                    if (entity != null)
                    {
                        WriteLog($"Entity found in DB. Removing ID: {entity.Id}, Name: {entity.Name}");
                        db.Fences.Remove(entity);
                        var result = db.SaveChanges();
                        WriteLog($"SaveChanges result: {result}");
                    }
                    else
                    {
                        WriteLog("Entity NOT found in DB with that ID.");
                    }
                }
            }
            else
            {
                WriteLog("Error: Fence Window Tag (ID) is null.");
                MessageBox.Show("Error: No se pudo identificar el Fence en la BD (Tag nulo).");
            }
            window.Close();
            // Notify UI
            FencesUpdated?.Invoke();
        }
        else
        {
            WriteLog("Could not find open window for this ViewModel.");
        }
    }

    public static Dictionary<string, string[]> GetDefaultCategories()
    {
        return new Dictionary<string, string[]>
        {
            { "📄 Documentos", new[] { ".pdf", ".doc", ".docx", ".txt", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".rtf", ".csv" } },
            { "🖼️ Imágenes", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff", ".psd" } },
            { "🎬 Videos", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" } },
            { "🎵 Música", new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" } },
            { "⚙️ Aplicaciones", new[] { ".exe", ".lnk", ".msi", ".bat", ".cmd", ".ps1", ".appx" } },
            { "📦 Comprimidos", new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz" } },
        };
    }

    /// <summary>
    /// Get all fence configurations from DB
    /// </summary>
    public List<FenceConfiguration> GetAllFences()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
            return db.Fences.ToList();
        }
    }

    /// <summary>
    /// Update rules for a specific fence and refresh it live
    /// </summary>
    public void UpdateFenceRules(int fenceId, string newExtensions)
    {
        // 1. Update in DB
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
            var fence = db.Fences.Find(fenceId);
            if (fence != null)
            {
                fence.Extensions = newExtensions;
                db.SaveChanges();
            }
        }

        // 2. Find running fence and update it
        // We need to match by ID. Window.Tag holds the ID.
        var window = _openFences.FirstOrDefault(w => (w.Tag as int?) == fenceId);
        if (window != null && window.DataContext is FenceViewModel vm)
        {
            // We need a method on ViewModel to update extensions, OR just restart this fence.
            // Restarting is safer to ensure consistency.
            window.Close();
            // _openFences.Remove(window); // Close() event handler does this already

            // Re-create it
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
                var config = db.Fences.Find(fenceId);
                var prefsRepo = scope.ServiceProvider.GetRequiredService<Core.Interfaces.IRepository<UserPreferences>>();
                var prefs = prefsRepo.GetAllAsync().Result.FirstOrDefault() ?? new UserPreferences();
                
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (config != null)
                {
                    if (config.Category == "Others")
                    {
                        CreateOthersFence(config, desktopPath, prefs);
                    }
                    else
                    {
                        var extensions = config.Extensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        CreateFenceFromConfig(config, desktopPath, prefs, extensions);
                    }
                }
            }
            FencesUpdated?.Invoke();
        }
    }
    public void UpdateFenceAppearance(string hexColor, double opacity)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var window in _openFences)
            {
                if (window.DataContext is FenceViewModel vm)
                {
                    vm.HexColor = hexColor;
                    vm.Opacity = opacity;
                }
            }
        });
    }

    public FenceWindow? GetFenceWindowAtPoint(Point screenPoint)
    {
        foreach (var window in _openFences)
        {
            if (window.Visibility != Visibility.Visible) continue;

            if (screenPoint.X >= window.Left && screenPoint.X <= window.Left + window.Width &&
                screenPoint.Y >= window.Top && screenPoint.Y <= window.Top + window.Height)
            {
                return window;
            }
        }
        return null;
    }

    public void CreateCustomFence(double x, double y, double w, double h)
    {
        // Use CreateFenceFromRect logic but simplified
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DesktopOrganizer.Data.Context.DesktopOrganizerDbContext>();
            var newFence = new DesktopOrganizer.Core.Models.FenceConfiguration
            {
                Name = "New Fence",
                Left = x,
                Top = y,
                Width = w,
                Height = h,
                Category = "User",
                Extensions = ""
            };
            db.Fences.Add(newFence);
            db.SaveChanges();

            var prefsRepo = scope.ServiceProvider.GetRequiredService<Core.Interfaces.IRepository<UserPreferences>>();
            var prefs = prefsRepo.GetAllAsync().Result.FirstOrDefault() ?? new UserPreferences();
            
            Application.Current.Dispatcher.Invoke(() => {
                CreateFenceFromConfig(newFence, Environment.GetFolderPath(Environment.SpecialFolder.Desktop), prefs, new string[0]);
            });
        }
    }
}
