using System.Collections.Generic;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class RepositoryCsvTests : TestBase
{
    private static readonly string[] TwoRepos = ["owner/repo-a", "owner/repo-b"];
    private static readonly string[] ExactDuplicates = ["owner/repo-a", "owner/repo-a"];
    private static readonly string[] CaseDifferingDuplicates = ["owner/repo-a", "Owner/Repo-A"];
    private static readonly string[] NoRepos = [];

    public static TheoryData<IReadOnlyList<string>, string> BuildCases() =>
        new()
        {
            { TwoRepos, "owner/repo-a,owner/repo-b" },
            { ExactDuplicates, "owner/repo-a" },
            { CaseDifferingDuplicates, "owner/repo-a" },
            { NoRepos, string.Empty },
        };

    [Theory]
    [MemberData(nameof(BuildCases))]
    public void Build_ReturnsExpectedCsv(IReadOnlyList<string> repositories, string expected)
    {
        string result = RepositoryCsv.Build(repositories);

        Assert.Equal(expected: expected, actual: result);
    }
}
