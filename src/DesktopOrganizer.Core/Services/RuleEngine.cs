using System.Collections.Generic;
using System.Linq;
using DesktopOrganizer.Core.Interfaces;

namespace DesktopOrganizer.Core.Services;

public class RuleEngine
{
    private readonly List<IRule> _rules;

    public RuleEngine(IEnumerable<IRule> rules)
    {
        _rules = rules.OrderBy(r => r.Priority).ToList();
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
