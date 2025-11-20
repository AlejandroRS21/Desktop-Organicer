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
    private Rule? _selectedRule;
    private string _statusMessage = "";

    public ObservableCollection<Rule> Rules { get; } = new();

    public Rule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            _selectedRule = value;
            OnPropertyChanged();
            (DeleteRuleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadRulesCommand { get; }
    public ICommand AddRuleCommand { get; }
    public ICommand DeleteRuleCommand { get; }
    public ICommand SaveRuleCommand { get; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public RuleEditorViewModel(IRepository<Rule> ruleRepository)
    {
        _ruleRepository = ruleRepository;

        LoadRulesCommand = new RelayCommand(async _ => await LoadRulesAsync());
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
        foreach (var rule in rules)
        {
            Rules.Add(rule);
        }
        StatusMessage = "Rules loaded.";
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
