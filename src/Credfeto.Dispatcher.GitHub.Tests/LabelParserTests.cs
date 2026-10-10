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
}
