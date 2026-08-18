using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Server.Helpers;
using Credfeto.Docker.HealthCheck.Http.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging.Abstractions;

namespace Credfeto.Dispatcher.Server;

internal static class Program
{
    private const int MIN_THREADS = 32;

    // Must be shorter than the Dockerfile HEALTHCHECK --timeout, otherwise Docker kills the probe before the client can report failure.
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(1.5);

    public static async Task<int> Main(string[] args)
    {
        return HealthCheckClient.IsHealthCheck(args: args, out string? checkUrl)
            ? await HealthCheckClient.ExecuteAsync(
                targetUrl: checkUrl,
                timeout: HealthCheckTimeout,
                logger: NullLogger.Instance,
                cancellationToken: CancellationToken.None
            )
            : await RunServerAsync(args);
    }

    private static async Task<int> RunServerAsync(string[] args)
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
