using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class RepositoryCsvTests : TestBase
{
    [Fact]
    public void Build_WithNoDuplicates_JoinsAllRepositories()
    {
        string result = RepositoryCsv.Build(["owner/repo-a", "owner/repo-b"]);

        Assert.Equal(expected: "owner/repo-a,owner/repo-b", actual: result);
    }

    [Fact]
    public void Build_WithExactDuplicates_CollapsesToOneEntry()
    {
        string result = RepositoryCsv.Build(["owner/repo-a", "owner/repo-a"]);

        Assert.Equal(expected: "owner/repo-a", actual: result);
    }

    [Fact]
    public void Build_WithCaseDifferingDuplicates_CollapsesToOneEntry()
    {
        string result = RepositoryCsv.Build(["owner/repo-a", "Owner/Repo-A"]);

        Assert.Equal(expected: "owner/repo-a", actual: result);
    }

    [Fact]
    public void Build_WithEmptyList_ReturnsEmptyString()
    {
        string result = RepositoryCsv.Build([]);

        Assert.Equal(expected: string.Empty, actual: result);
    }
}
