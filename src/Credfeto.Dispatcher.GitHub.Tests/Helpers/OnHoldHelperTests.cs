using Credfeto.Dispatcher.GitHub.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

public sealed class OnHoldHelperTests : TestBase
{
    [Fact]
    public void IsOnHoldReturnsTrueWhenNoWorkLabelMatches()
    {
        bool onHold = OnHoldHelper.IsOnHold(labels: ["on-hold"], noWorkFilters: ["On Hold", "on-hold"], labelFilters: []);

        Assert.True(onHold, userMessage: "Expected NoWorkFilter label match to mark item as OnHold");
    }

    [Fact]
    public void IsOnHoldReturnsTrueWhenRequiredLabelIsMissing()
    {
        bool onHold = OnHoldHelper.IsOnHold(labels: ["bug"], noWorkFilters: [], labelFilters: ["AI-Work"]);

        Assert.True(onHold, userMessage: "Expected item without any required LabelFilter label to be OnHold");
    }

    [Fact]
    public void IsOnHoldReturnsFalseWhenRequiredLabelExists()
    {
        bool onHold = OnHoldHelper.IsOnHold(labels: ["AI-Work", "bug"], noWorkFilters: [], labelFilters: ["ai-work"]);

        Assert.False(onHold, userMessage: "Expected item with a required LabelFilter label to not be OnHold");
    }

    [Fact]
    public void IsOnHoldReturnsFalseWhenNoFiltersConfigured()
    {
        bool onHold = OnHoldHelper.IsOnHold(labels: ["bug"], noWorkFilters: [], labelFilters: []);

        Assert.False(onHold, userMessage: "Expected item to not be OnHold when no filters are configured");
    }
}


