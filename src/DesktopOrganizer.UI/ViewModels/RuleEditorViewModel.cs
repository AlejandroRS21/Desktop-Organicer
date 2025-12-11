using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Rules;

namespace DesktopOrganizer.UI.ViewModels;

public class RuleEditorViewModel : INotifyPropertyChanged
{
    private readonly IRepository<Rule> _ruleRepository;
    private readonly DesktopOrganizer.UI.Services.FenceManager _fenceManager;
    private Rule? _selectedRule;
    private string _statusMessage = "";

    public ObservableCollection<Rule> Rules { get; } = new();

    // ... (properties)

    public RuleEditorViewModel(IRepository<Rule> ruleRepository, DesktopOrganizer.UI.Services.FenceManager fenceManager)
    {
        _ruleRepository = ruleRepository;
        _fenceManager = fenceManager;

        LoadRulesCommand = new RelayCommand(async _ => await LoadRulesAsync());
        // ... (rest of constructor)
        AddRuleCommand = new RelayCommand(async _ => await AddRuleAsync());
        DeleteRuleCommand = new RelayCommand(async _ => await DeleteRuleAsync(), _ => SelectedRule != null);
        SaveRuleCommand = new RelayCommand(async _ => await SaveRulesAsync());

        // Initial load
        _ = LoadRulesAsync();
    }

    private async Task LoadRulesAsync()
    {
        StatusMessage = "Loading rules...";
        Rules.Clear();
        var rules = await _ruleRepository.GetAllAsync();
        var fences = _fenceManager.GetAllFences();

        foreach (var rule in rules)
        {
            // Filter: Only show rules that correspond to an active fence
            // This prevents "Ghost Fences" (rules for deleted fences) from appearing
            
            bool hasMatchingFence = fences.Any(f => 
                // Check exact name match
                f.Name == rule.Name || 
                // Check if fence name contains rule name (handles emojis like "🎵 Música" vs "Música")
                f.Name.Contains(rule.Name) ||
                // Check internal category match
                f.Category == rule.TargetCategory
            );

            // If it's a "System" rule that organizes files, maybe we still want it?
            // User specially complained about "musica y comprimidos que ya eliminé".
            // So if they deleted the fence, they want the rule gone/hidden.
            
            if (hasMatchingFence)
            {
                Rules.Add(rule);
            }
        }
        StatusMessage = "Rules loaded (Synced with Fences).";
    }

    private async Task AddRuleAsync()
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

    private async Task DeleteRuleAsync()
    {
        if (SelectedRule == null) return;

        await _ruleRepository.DeleteAsync(SelectedRule);
        Rules.Remove(SelectedRule);
        SelectedRule = null;
        StatusMessage = "Rule deleted.";
    }

    private async Task SaveRulesAsync()
    {
        if (SelectedRule != null)
        {
            await _ruleRepository.UpdateAsync(SelectedRule);
            StatusMessage = "Rule saved.";
        }
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
