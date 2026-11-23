using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Services;

// Adoption rules for bot PRs (dependency updates and the like), shared by the full scan and the single-item
// refresh so both store the same priority and adoption flag for the same PR.
internal static class PullRequestAdoption
{
    private const string BOT_SUFFIX = "[bot]";

    public static bool IsAdopted(IReadOnlyList<BotPrRule> rules, string? author, string? headRef)
    {
        return FindRule(rules: rules, author: author, headRef: headRef) is not null;
    }

    public static WorkPriority CalculatePriority(
        IReadOnlyList<BotPrRule> rules,
        string? author,
        string? headRef,
        DateTimeOffset? createdAt,
        WorkPriority labelPriority,
        in DateTimeOffset now
    )
    {
        BotPrRule? matchingRule = FindRule(rules: rules, author: author, headRef: headRef);

        if (matchingRule is null)
        {
            return labelPriority;
        }

        bool timedOut = createdAt is { } created && now - created >= TimeSpan.FromHours(matchingRule.TimeoutHours);

        return HighestPriority(timedOut ? matchingRule.Priority : matchingRule.DefaultPriority, labelPriority);
    }

    private static BotPrRule? FindRule(IReadOnlyList<BotPrRule> rules, string? author, string? headRef)
    {
        return rules.FirstOrDefault(rule => MatchesRule(rule: rule, author: author, headRef: headRef));
    }

    private static WorkPriority HighestPriority(WorkPriority first, WorkPriority second)
    {
        return first < second ? second : first;
    }

    // REST reports a bot as "dependabot[bot]", GraphQL (the detail fetch) as "dependabot"; both name the same bot.
    private static bool AuthorMatches(string? author, string ruleAuthor)
    {
        return author is not null
            && StringComparer.OrdinalIgnoreCase.Equals(WithoutBotSuffix(author), WithoutBotSuffix(ruleAuthor));
    }

    private static string WithoutBotSuffix(string login)
    {
        return login.EndsWith(value: BOT_SUFFIX, comparisonType: StringComparison.OrdinalIgnoreCase)
            ? login[..^BOT_SUFFIX.Length]
            : login;
    }

    private static bool MatchesRule(BotPrRule rule, string? author, string? headRef)
    {
        if (rule.TimeoutHours <= 0)
        {
            return false;
        }

        if (!AuthorMatches(author: author, ruleAuthor: rule.Author))
        {
            return false;
        }

        return string.IsNullOrEmpty(rule.BranchPrefix)
            || !string.IsNullOrEmpty(headRef)
                && headRef.StartsWith(value: rule.BranchPrefix, comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
