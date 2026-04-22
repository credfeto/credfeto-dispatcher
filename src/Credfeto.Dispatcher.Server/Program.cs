using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Server.Helpers;
using Microsoft.Extensions.Hosting;

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
            using (IHost app = ServerStartup.CreateApp(args))
            {
                await RunAsync(app);

                return 0;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("An error occurred:");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);

            return 1;
        }
    }

    private static Task RunAsync(IHost application)
    {
        Console.WriteLine("App Created");

        return application.RunAsync(CancellationToken.None);
    }
}
