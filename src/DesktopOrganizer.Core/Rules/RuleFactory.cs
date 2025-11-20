using System;
using System.Text.Json;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Core.Rules;

public static class RuleFactory
{
    public static IRule? CreateRule(Rule ruleEntity)
    {
        if (!ruleEntity.IsActive) return null;

        switch (ruleEntity.RuleType)
        {
            case nameof(ExtensionRule):
                var extRule = new ExtensionRule
                {
                    Name = ruleEntity.Name,
                    Priority = ruleEntity.Priority,
                    IsActive = ruleEntity.IsActive,
                    TargetCategory = ruleEntity.TargetCategory
                };
                
                if (!string.IsNullOrEmpty(ruleEntity.Configuration))
                {
                    try 
                    {
                        var config = JsonSerializer.Deserialize<string[]>(ruleEntity.Configuration);
                        if (config != null) extRule.Extensions = config;
                    }
                    catch { /* Log error */ }
                }
                return extRule;

            case nameof(NamePatternRule):
                var patternRule = new NamePatternRule
                {
                    Name = ruleEntity.Name,
                    Priority = ruleEntity.Priority,
                    IsActive = ruleEntity.IsActive,
                    TargetCategory = ruleEntity.TargetCategory
                };
                
                if (!string.IsNullOrEmpty(ruleEntity.Configuration))
                {
                    patternRule.Pattern = ruleEntity.Configuration; // Assuming config is just the pattern string for now
                }
                return patternRule;

            default:
                return null;
        }
    }
}
