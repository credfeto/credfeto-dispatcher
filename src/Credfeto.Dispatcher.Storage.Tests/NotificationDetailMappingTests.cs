using System.Collections.Generic;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class NotificationDetailMappingTests : TestBase
{
    private static readonly string[] NoAuthors = [];
    private static readonly string[] OneAuthor = ["octocat"];
    private static readonly string[] DuplicateAuthors = ["octocat", "OctoCat", "someone-else"];

    public static TheoryData<IReadOnlyList<string>, string?> BuildCommitAuthorsCases() =>
        new()
        {
            { NoAuthors, null },
            { OneAuthor, "octocat" },
            { DuplicateAuthors, "octocat,someone-else" },
        };

    [Theory]
    [MemberData(nameof(BuildCommitAuthorsCases))]
    public void BuildCommitAuthors_ReturnsNullForUnknownAndCsvOtherwise(
        IReadOnlyList<string> commitAuthors,
        string? expected
    )
    {
        string? result = NotificationDetailMapping.BuildCommitAuthors(commitAuthors);

        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public void KnownCommitAuthors_ReturnsNullForEmptyList()
    {
        Assert.Null(NotificationDetailMapping.KnownCommitAuthors(NoAuthors));
    }

    [Fact]
    public void KnownCommitAuthors_ReturnsListWhenNotEmpty()
    {
        Assert.Same(expected: OneAuthor, actual: NotificationDetailMapping.KnownCommitAuthors(OneAuthor));
    }
}
