using System;
using System.Collections.Generic;
using System.Linq;

namespace Credfeto.Dispatcher.Storage;

internal static class RepositoryCsv
{
    public static string Build(IReadOnlyList<string> repositories)
    {
        return string.Join(separator: ',', repositories.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
