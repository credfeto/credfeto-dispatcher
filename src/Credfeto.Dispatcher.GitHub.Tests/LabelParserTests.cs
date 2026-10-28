using Credfeto.Dispatcher.GitHub.DataTypes;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class LabelParserTests : TestBase
{
    [Theory]
    [InlineData("Security", WorkPriority.SECURITY)]
    [InlineData("Priority: Urgent", WorkPriority.URGENT)]
    [InlineData("Priority: High", WorkPriority.HIGH)]
    [InlineData("Priority: Medium", WorkPriority.MEDIUM)]
    [InlineData("Priority: Low", WorkPriority.LOW)]
    [InlineData("enhancement", WorkPriority.UNKNOWN)]
    [InlineData("SECURITY", WorkPriority.SECURITY)]
    [InlineData("uRgEnT", WorkPriority.URGENT)]
    [InlineData("HIGH", WorkPriority.HIGH)]
    [InlineData("MeDiUm", WorkPriority.MEDIUM)]
    [InlineData("LOW", WorkPriority.LOW)]
    [InlineData("needs-security-review", WorkPriority.SECURITY)]
    [InlineData("very urgent fix", WorkPriority.URGENT)]
    [InlineData("high security review", WorkPriority.SECURITY)]
    [InlineData("priority_medium_ish", WorkPriority.MEDIUM)]
    [InlineData("lowest", WorkPriority.LOW)]
    public static void ParsePriorityMapsLabelToPriority(string label, WorkPriority expected)
    {
        WorkPriority actual = LabelParser.ParsePriority([label]);

        Assert.Equal(expected: expected, actual: actual);
    }

    [Fact]
    public static void ParsePriorityReturnsUnknownWhenThereAreNoLabels()
    {
        WorkPriority actual = LabelParser.ParsePriority([]);

        Assert.Equal(expected: WorkPriority.UNKNOWN, actual: actual);
    }

    [Fact]
    public static void ParsePriorityReturnsUnknownWhenNoLabelMatches()
    {
        WorkPriority actual = LabelParser.ParsePriority(["bug", "enhancement", "documentation"]);

        Assert.Equal(expected: WorkPriority.UNKNOWN, actual: actual);
    }

    [Fact]
    public static void ParsePriorityReturnsSecurityWhenLowPriorityLabelComesFirst()
    {
        WorkPriority actual = LabelParser.ParsePriority(["low priority", "security"]);

        Assert.Equal(expected: WorkPriority.SECURITY, actual: actual);
    }

    [Theory]
    [InlineData("medium", "urgent", WorkPriority.URGENT)]
    [InlineData("low", "high", WorkPriority.HIGH)]
    [InlineData("low", "medium", WorkPriority.MEDIUM)]
    [InlineData("high", "security", WorkPriority.SECURITY)]
    [InlineData("bug", "low", WorkPriority.LOW)]
    public static void ParsePriorityReturnsHighestPriorityRegardlessOfLabelOrder(
        string first,
        string second,
        WorkPriority expected
    )
    {
        WorkPriority forwards = LabelParser.ParsePriority([first, second]);
        WorkPriority backwards = LabelParser.ParsePriority([second, first]);

        Assert.Equal(expected: expected, actual: forwards);
        Assert.Equal(expected: expected, actual: backwards);
    }

    [Fact]
    public static void ParsePriorityReturnsHighestPriorityAcrossManyLabels()
    {
        WorkPriority actual = LabelParser.ParsePriority(["bug", "low", "medium", "urgent", "high"]);

        Assert.Equal(expected: WorkPriority.URGENT, actual: actual);
    }
}
