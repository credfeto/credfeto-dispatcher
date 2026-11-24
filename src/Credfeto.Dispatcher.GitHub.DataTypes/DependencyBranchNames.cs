using System;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

// Dependency-update branches: the depends/* branches the dependency tooling pushes and dependabot/* branches.
public static class DependencyBranchNames
{
    public static bool IsDependencyBranch([NotNullWhen(true)] string? branch)
    {
        return !string.IsNullOrEmpty(branch)
            && (
                branch.StartsWith(value: "depends/", comparisonType: StringComparison.OrdinalIgnoreCase)
                || branch.StartsWith(value: "dependabot/", comparisonType: StringComparison.OrdinalIgnoreCase)
            );
    }
}
