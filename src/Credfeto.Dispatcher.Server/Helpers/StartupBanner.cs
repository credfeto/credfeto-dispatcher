using System;
using Figgle;

namespace Credfeto.Dispatcher.Server.Helpers;

// https://www.figlet.org/examples.html
[GenerateFiggleText(memberName: "Banner", fontName: "basic", sourceText: "Dispatcher")]
internal static partial class StartupBanner
{
    public static void Show()
    {
        Console.WriteLine(Banner);
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"{VersionInformation.Product} ({VersionInformation.Version}): Starting...");
        Console.WriteLine(string.Empty);
    }
}
