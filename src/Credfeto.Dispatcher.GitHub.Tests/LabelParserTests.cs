using System;
using Credfeto.Dispatcher.GitHub.DataTypes;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class LabelParserTests : TestBase
{
    [Fact]
    public void ParsePriorityReturnsUnknownForEmptyList() =>
        AssertPriority(WorkPriority.Unknown, []);

    [Fact]
    public void ParsePriorityReturnsSecurityWhenSecurityAndUrgentBothPresent() =>
        AssertPriority(WorkPriority.Security, ["Urgent", "Security"]);

    [Fact]
    public void ParsePriorityReturnsUnknownForIrrelevantLabels() =>
        AssertPriority(WorkPriority.Unknown, ["bug", "enhancement"]);

    [Fact]
    public void SecurityRanksHigherThanUrgentNumerically() =>
        Assert.True(
            WorkPriority.Security > WorkPriority.Urgent,
            userMessage: "Security priority must be numerically higher than Urgent"
        );

    [Theory]
    [InlineData("Security", WorkPriority.Security)]
    [InlineData("security", WorkPriority.Security)]
    [InlineData("SECURITY", WorkPriority.Security)]
    [InlineData("Urgent", WorkPriority.Urgent)]
    [InlineData("urgent", WorkPriority.Urgent)]
    [InlineData("URGENT", WorkPriority.Urgent)]
    [InlineData("High", WorkPriority.High)]
    [InlineData("Medium", WorkPriority.Medium)]
    [InlineData("Low", WorkPriority.Low)]
    public void ParsePriorityReturnsExpectedForSingleLabel(string label, WorkPriority expected) =>
        AssertPriority(expected, [label]);

    private static void AssertPriority(WorkPriority expected, in ReadOnlySpan<string> labels)
    {
        WorkPriority result = LabelParser.ParsePriority(labels.ToArray());

        Assert.Equal(expected: expected, actual: result);
    }
}
