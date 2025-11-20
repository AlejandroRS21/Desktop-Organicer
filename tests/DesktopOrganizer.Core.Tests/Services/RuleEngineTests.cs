using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.Core.Interfaces;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Rules;

namespace DesktopOrganizer.Core.Tests.Services;

public class RuleEngineTests
{
    [Fact]
    public async Task EvaluateFile_ShouldReturnTargetCategory_WhenRuleMatches()
    {
        // Arrange
        var ruleRepoMock = new Mock<IRepository<Rule>>();
        var rules = new List<Rule>
        {
            new Rule 
            { 
                Name = "TestRule",
                Priority = 1,
                IsActive = true,
                TargetCategory = "Documents",
                RuleType = nameof(ExtensionRule),
                Configuration = "[\".pdf\"]"
            }
        };

        ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rules);

        var ruleEngine = new RuleEngine(ruleRepoMock.Object);
        await ruleEngine.LoadRulesAsync();

        // Act
        var result = ruleEngine.EvaluateFile("test.pdf");

        // Assert
        Assert.Equal("Documents", result);
    }

    [Fact]
    public async Task EvaluateFile_ShouldReturnNull_WhenNoRuleMatches()
    {
        // Arrange
        var ruleRepoMock = new Mock<IRepository<Rule>>();
        var rules = new List<Rule>
        {
            new Rule 
            { 
                Name = "TestRule",
                Priority = 1,
                IsActive = true,
                TargetCategory = "Documents",
                RuleType = nameof(ExtensionRule),
                Configuration = "[\".docx\"]"
            }
        };

        ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rules);

        var ruleEngine = new RuleEngine(ruleRepoMock.Object);
        await ruleEngine.LoadRulesAsync();

        // Act
        var result = ruleEngine.EvaluateFile("test.pdf");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateFile_ShouldRespectPriority()
    {
        // Arrange
        var ruleRepoMock = new Mock<IRepository<Rule>>();
        var rules = new List<Rule>
        {
            new Rule 
            { 
                Name = "LowPriority",
                Priority = 10,
                IsActive = true,
                TargetCategory = "LowPriority",
                RuleType = nameof(ExtensionRule),
                Configuration = "[\".pdf\"]"
            },
            new Rule 
            { 
                Name = "HighPriority",
                Priority = 1,
                IsActive = true,
                TargetCategory = "HighPriority",
                RuleType = nameof(ExtensionRule),
                Configuration = "[\".pdf\"]"
            }
        };

        ruleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rules);

        var ruleEngine = new RuleEngine(ruleRepoMock.Object);
        await ruleEngine.LoadRulesAsync();

        // Act
        var result = ruleEngine.EvaluateFile("test.pdf");

        // Assert
        Assert.Equal("HighPriority", result);
    }
}
