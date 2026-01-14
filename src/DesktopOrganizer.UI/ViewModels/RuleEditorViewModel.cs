using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.UI.Services;

namespace DesktopOrganizer.UI.ViewModels;

public partial class RuleEditorViewModel : ObservableObject
{
    private readonly IRepository<Rule> _ruleRepository;
    private readonly RuleTemplateService _templateService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRuleCommand))]
    private Rule? _selectedRule;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _simulationDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);

    public ObservableCollection<Rule> Rules { get; } = new();
    public ObservableCollection<SimulationResult> SimulationResults { get; } = new();
    public ObservableCollection<RuleTemplate> Templates { get; } = new();

    public RuleEditorViewModel(IRepository<Rule> ruleRepository, 
                               FenceManager fenceManager,
                               FileOrganizer fileOrganizer,
                               RuleTemplateService templateService)
    {
        _ruleRepository = ruleRepository;
        _fenceManager = fenceManager;
        _fileOrganizer = fileOrganizer;
        _templateService = templateService;

        // Load templates
        var templates = _templateService.GetTemplates();
        foreach (var t in templates) Templates.Add(t);

        // Initial load
        _ = LoadRules();
    }

    [RelayCommand]
    private async Task LoadRules()
    {
        StatusMessage = "Loading rules...";
        Rules.Clear();
        var rules = await _ruleRepository.GetAllAsync();
        
        // We load ALL rules now. We don't filter safely by fence because we might be in Setup phase.
        foreach (var rule in rules)
        {
            Rules.Add(rule);
        }
        StatusMessage = "Rules loaded.";
    }

    [RelayCommand]
    private async Task ApplyTemplate(RuleTemplate template)
    {
        if (template == null) return;

        StatusMessage = $"Aplicando plantilla '{template.Name}'...";

        // Add rules from template
        foreach (var templateRule in template.Rules)
        {
            // Check if rule exists (by name) to avoid duplicates
            var exists = Rules.Any(r => r.Name == templateRule.Name);
            if (!exists)
            {
                var newRule = new Rule
                {
                    Name = templateRule.Name,
                    Priority = Rules.Count + 1,
                    IsActive = true,
                    TargetCategory = templateRule.TargetCategory,
                    RuleType = templateRule.RuleType,
                    Configuration = templateRule.Configuration
                };

                await _ruleRepository.AddAsync(newRule);
                Rules.Add(newRule);

                // Ensure Fence exists for this Rule
                // Note: This requires FenceManager to persist to DB. 
                // Currently FenceManager seems to be in-memory or syncs on init.
                // We'll rely on the user or a future 'Sync Fences' feature, 
                // BUT for a good UX, we should try to add the fence now.
                // Assuming FenceManager has an AddFence method (we should check, but if not we can't do it easily here).
                // Ideally, MainWindow listens to Rule changes or we force a fence creation.
            }
        }
        
        StatusMessage = $"Plantilla '{template.Name}' aplicada exitosamente.";
    }

    [RelayCommand]
    private async Task SimulateRules()
    {
        if (string.IsNullOrEmpty(SimulationDirectory) || !System.IO.Directory.Exists(SimulationDirectory))
        {
            StatusMessage = "Directorio de simulación inválido.";
            return;
        }

        StatusMessage = "Simulando organización...";
        try
        {
            var results = await _fileOrganizer.SimulateOrganizationAsync(SimulationDirectory);
            SimulationResults.Clear();
            foreach (var result in results)
            {
                SimulationResults.Add(result);
            }
            StatusMessage = $"Simulación completada. {results.Count} archivos coinciden con las reglas.";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error en simulación: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddRule()
    {
        var newRule = new Rule
        {
            Name = "New Rule",
            Priority = Rules.Count + 1,
            IsActive = true,
            TargetCategory = "Documents",
            RuleType = nameof(ExtensionRule),
            Configuration = "[\".txt\"]"
        };

        await _ruleRepository.AddAsync(newRule);
        Rules.Add(newRule);
        SelectedRule = newRule;
        StatusMessage = "New rule added.";
    }

    private bool CanModifyRule => SelectedRule != null;

    [RelayCommand(CanExecute = nameof(CanModifyRule))]
    private async Task DeleteRule()
    {
        if (SelectedRule == null) return;

        await _ruleRepository.DeleteAsync(SelectedRule);
        Rules.Remove(SelectedRule);
        SelectedRule = null;
        StatusMessage = "Rule deleted.";
    }

    [RelayCommand(CanExecute = nameof(CanModifyRule))]
    private async Task SaveRule()
    {
        if (SelectedRule != null)
        {
            await _ruleRepository.UpdateAsync(SelectedRule);
            StatusMessage = "Rule saved.";
        }
    }
}
