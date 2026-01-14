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

namespace DesktopOrganizer.UI.ViewModels;

public partial class RuleEditorViewModel : ObservableObject
{
    private readonly IRepository<Rule> _ruleRepository;
    private readonly DesktopOrganizer.UI.Services.FenceManager _fenceManager;
    private readonly DesktopOrganizer.Core.Services.FileOrganizer _fileOrganizer;

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

    public RuleEditorViewModel(IRepository<Rule> ruleRepository, 
                               DesktopOrganizer.UI.Services.FenceManager fenceManager,
                               DesktopOrganizer.Core.Services.FileOrganizer fileOrganizer)
    {
        _ruleRepository = ruleRepository;
        _fenceManager = fenceManager;
        _fileOrganizer = fileOrganizer;

        // Initial load
        _ = LoadRules();
    }

    [RelayCommand]
    private async Task LoadRules()
    {
        StatusMessage = "Loading rules...";
        Rules.Clear();
        var rules = await _ruleRepository.GetAllAsync();
        var fences = _fenceManager.GetAllFences();

        foreach (var rule in rules)
        {
            // Filter: Only show rules that correspond to an active fence
            bool hasMatchingFence = fences.Any(f => 
                f.Name == rule.Name || 
                f.Name.Contains(rule.Name) ||
                f.Category == rule.TargetCategory
            );

            if (hasMatchingFence)
            {
                Rules.Add(rule);
            }
        }
        StatusMessage = "Rules loaded (Synced with Fences).";
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
