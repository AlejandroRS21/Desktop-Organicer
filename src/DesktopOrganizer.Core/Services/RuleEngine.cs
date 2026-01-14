using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Rules;

namespace DesktopOrganizer.Core.Services;

public class RuleEngine
{
    private readonly List<IRule> _rules = new();
    private readonly IRepository<Rule> _ruleRepository;

    public IReadOnlyList<IRule> Rules => _rules;

    public RuleEngine(IRepository<Rule> ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task LoadRulesAsync()
    {
        _rules.Clear();
        var ruleEntities = await _ruleRepository.GetAllAsync();
        
        foreach (var entity in ruleEntities)
        {
            var rule = RuleFactory.CreateRule(entity);
            if (rule != null)
            {
                _rules.Add(rule);
            }
        }
        
        _rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public string? EvaluateFile(string filePath)
    {
        foreach (var rule in _rules)
        {
            if (rule.Evaluate(filePath))
            {
                return rule.TargetCategory;
            }
        }
        
        return null; // No matching rule found
    }

    public void AddRule(IRule rule)
    {
        _rules.Add(rule);
        // Re-sort by priority
        _rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
    
    public void RemoveRule(IRule rule)
    {
        _rules.Remove(rule);
    }
}
