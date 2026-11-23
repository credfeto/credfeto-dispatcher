using System;
using System.Collections.Generic;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class PullRequestAdoptionTests : TestBase
{
    private static readonly DateTimeOffset Now = new(
        year: 2026,
        month: 11,
        day: 23,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private static readonly IReadOnlyList<BotPrRule> Rules =
    [
        new BotPrRule
        {
            Author = "dependabot[bot]",
            BranchPrefix = "dependabot/",
            DefaultPriority = WorkPriority.MEDIUM,
            TimeoutHours = 24,
            Priority = WorkPriority.SECURITY,
        },
    ];

    [Theory]
    [InlineData("dependabot[bot]", "dependabot/nuget/x", true)]
    [InlineData("dependabot", "dependabot/nuget/x", true)]
    [InlineData("Dependabot[Bot]", "dependabot/nuget/x", true)]
    [InlineData("dependabot[bot]", "feature/x", false)]
    [InlineData("dependabot[bot]", null, false)]
    [InlineData("someone", "dependabot/nuget/x", false)]
    [InlineData(null, "dependabot/nuget/x", false)]
    public static void IsAdoptedMatchesTheRuleAuthorAndBranch(string? author, string? headRef, bool expected)
    {
        Assert.Equal(
            expected: expected,
            actual: PullRequestAdoption.IsAdopted(rules: Rules, author: author, headRef: headRef)
        );
    }

    [Fact]
    public static void RuleWithoutTimeoutNeverMatches()
    {
        IReadOnlyList<BotPrRule> rules = [new BotPrRule { Author = "dependabot[bot]", TimeoutHours = 0 }];

        Assert.False(
            PullRequestAdoption.IsAdopted(rules: rules, author: "dependabot[bot]", headRef: "dependabot/x"),
            userMessage: "A rule with no timeout is disabled"
        );
    }

    [Fact]
    public static void UnadoptedPullRequestKeepsItsLabelPriority()
    {
        WorkPriority priority = PullRequestAdoption.CalculatePriority(
            rules: Rules,
            author: "someone",
            headRef: "feature/x",
            createdAt: Now.AddDays(-10),
            labelPriority: WorkPriority.LOW,
            now: Now
        );

        Assert.Equal(expected: WorkPriority.LOW, actual: priority);
    }

    [Fact]
    public static void AdoptedPullRequestGetsTheDefaultPriorityUntilItTimesOut()
    {
        WorkPriority priority = PullRequestAdoption.CalculatePriority(
            rules: Rules,
            author: "dependabot",
            headRef: "dependabot/nuget/x",
            createdAt: Now.AddHours(-1),
            labelPriority: WorkPriority.UNKNOWN,
            now: Now
        );

        Assert.Equal(expected: WorkPriority.MEDIUM, actual: priority);
    }

    [Fact]
    public static void AdoptedPullRequestPastItsTimeoutGetsTheEscalatedPriority()
    {
        WorkPriority priority = PullRequestAdoption.CalculatePriority(
            rules: Rules,
            author: "dependabot[bot]",
            headRef: "dependabot/nuget/x",
            createdAt: Now.AddHours(-24),
            labelPriority: WorkPriority.UNKNOWN,
            now: Now
        );

        Assert.Equal(expected: WorkPriority.SECURITY, actual: priority);
    }

    [Fact]
    public static void AdoptedPullRequestWithUnknownAgeDoesNotEscalate()
    {
        WorkPriority priority = PullRequestAdoption.CalculatePriority(
            rules: Rules,
            author: "dependabot[bot]",
            headRef: "dependabot/nuget/x",
            createdAt: null,
            labelPriority: WorkPriority.HIGH,
            now: Now
        );

        Assert.Equal(expected: WorkPriority.HIGH, actual: priority);
    }
}
