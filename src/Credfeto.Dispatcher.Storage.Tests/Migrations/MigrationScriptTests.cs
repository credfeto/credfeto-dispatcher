using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Migrations;

public sealed class MigrationScriptTests : TestBase
{
    private static readonly Assembly StorageAssembly = typeof(DatabaseMigrationService).Assembly;

    private static IReadOnlyList<string> GetMigrationResourceNames()
    {
        return
        [
            .. StorageAssembly
                .GetManifestResourceNames()
                .Where(n => n.EndsWith(value: ".sql", comparisonType: System.StringComparison.OrdinalIgnoreCase)),
        ];
    }

    private static string ExtractPrefix(string resourceName)
    {
        int lastDot = resourceName.LastIndexOf('.');
        string fileName = lastDot >= 0 ? resourceName[..lastDot] : resourceName;

        int lastSeparator = fileName.LastIndexOf('.');
        string scriptName = lastSeparator >= 0 ? fileName[(lastSeparator + 1)..] : fileName;

        int underscore = scriptName.IndexOf('_', StringComparison.Ordinal);

        return underscore > 0 ? scriptName[..underscore] : scriptName;
    }

    [Fact]
    public void NoDuplicateMigrationPrefixes()
    {
        IReadOnlyList<string> resources = GetMigrationResourceNames();

        Assert.NotEmpty(resources);

        IEnumerable<IGrouping<string, string>> duplicates = resources
            .GroupBy(ExtractPrefix, StringComparer.Ordinal)
            .Where(g => g.Skip(1).Any());

        string[] duplicate = [.. duplicates.Select(g => g.Key)];

        Assert.Empty(duplicate);
    }

    [Fact]
    public void MigrationPrefixesAreSequential()
    {
        IReadOnlyList<string> resources = GetMigrationResourceNames();

        Assert.NotEmpty(resources);

        int[] prefixes =
        [
            .. resources
                .Select(ExtractPrefix)
                .Where(p =>
                    int.TryParse(p, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out _)
                )
                .Select(p => int.Parse(p, style: NumberStyles.None, provider: CultureInfo.InvariantCulture))
                .Order(),
        ];

        for (int i = 0; i < prefixes.Length; i++)
        {
            Assert.Equal(expected: i + 1, actual: prefixes[i]);
        }
    }
}
