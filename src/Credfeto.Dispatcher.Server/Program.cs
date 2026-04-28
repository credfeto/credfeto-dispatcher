using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Server.Helpers;
using Microsoft.AspNetCore.Builder;

namespace Credfeto.Dispatcher.Server;

internal static class Program
{
    private const int MIN_THREADS = 32;

    public static async Task<int> Main(string[] args)
    {
        StartupBanner.Show();
        ServerStartup.SetThreads(MIN_THREADS);

        try
        {
            await using WebApplication app = ServerStartup.CreateApp(args);
            await app.RunAsync();

            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("An error occurred:");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);

            return 1;
        }
    }
}
