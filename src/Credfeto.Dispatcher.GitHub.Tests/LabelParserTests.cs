using Credfeto.Dispatcher.GitHub.DataTypes;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class LabelParserTests : TestBase
{
    [Fact]
    public void ParsePriorityReturnsUnknownForEmptyList()
    {
        WorkPriority result = LabelParser.ParsePriority([]);

        Assert.Equal(expected: WorkPriority.Unknown, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsSecurityForSecurityLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["Security"]);

        Assert.Equal(expected: WorkPriority.Security, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsSecurityForLowercaseSecurityLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["security"]);

        Assert.Equal(expected: WorkPriority.Security, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsSecurityWhenSecurityAndUrgentBothPresent()
    {
        WorkPriority result = LabelParser.ParsePriority(["Urgent", "Security"]);

        Assert.Equal(expected: WorkPriority.Security, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsUrgentForUrgentLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["Urgent"]);

        Assert.Equal(expected: WorkPriority.Urgent, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsHighForHighLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["High"]);

        Assert.Equal(expected: WorkPriority.High, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsMediumForMediumLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["Medium"]);

        Assert.Equal(expected: WorkPriority.Medium, actual: result);
    }

    [Fact]
    public void ParsePriorityReturnsLowForLowLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["Low"]);

        Assert.Equal(expected: WorkPriority.Low, actual: result);
    }

    [Theory]
    [InlineData("security")]
    [InlineData("Security")]
    [InlineData("SECURITY")]
    public void ParsePriorityReturnsSecurityCaseInsensitively(string label)
    {
        WorkPriority result = LabelParser.ParsePriority([label]);

        Assert.Equal(expected: WorkPriority.Security, actual: result);
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("Urgent")]
    [InlineData("URGENT")]
    public void ParsePriorityReturnsUrgentCaseInsensitively(string label)
    {
        WorkPriority result = LabelParser.ParsePriority([label]);

        Assert.Equal(expected: WorkPriority.Urgent, actual: result);
    }

    [Fact]
    public void ParsePrioritySecurityRanksHigherThanUrgentNumerically()
    {
        Assert.True(
            WorkPriority.Security > WorkPriority.Urgent,
            userMessage: "Security priority must be numerically higher than Urgent"
        );
    }

    [Fact]
    public void ParsePriorityReturnsUnknownForIrrelevantLabel()
    {
        WorkPriority result = LabelParser.ParsePriority(["bug", "enhancement"]);

        Assert.Equal(expected: WorkPriority.Unknown, actual: result);
    }
}
