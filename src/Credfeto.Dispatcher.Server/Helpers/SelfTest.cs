using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Credfeto.Dispatcher.Server.Helpers;

internal static class SelfTest
{
    public const string SWITCH = "--self-test";

    public static bool IsSelfTest(IReadOnlyList<string> args)
    {
        return args.Contains(value: SWITCH, comparer: StringComparer.Ordinal);
    }

    public static string[] WithoutSwitch(IReadOnlyList<string> args)
    {
        return [.. args.Where(static arg => !StringComparer.Ordinal.Equals(x: arg, y: SWITCH))];
    }

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            LoadNativeDependencies();
            UsePlaceholderSecrets();

            await using WebApplication app = ServerStartup.CreateApp(WithoutSwitch(args));

            int hostedServices = app.Services.GetServices<IHostedService>().Count();

            await Console.Out.WriteLineAsync(
                value: $"Self-test passed: {hostedServices} hosted services resolved",
                cancellationToken: app.Lifetime.ApplicationStopping
            );

            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(
                value: $"Self-test failed: {exception.GetType().Name}: {exception.Message}",
                cancellationToken: CancellationToken.None
            );

            return 1;
        }
    }

    private static void UsePlaceholderSecrets()
    {
        // The image is built without secrets, but options validation needs them to be set.
        // Same placeholders as scripts/smoke-test.sh; nothing is ever called with them.
        SetIfMissing(name: "GitHub__Token", value: "self-test-placeholder");
        SetIfMissing(name: "Discord__WebhookUrl", value: "http://localhost:1/self-test");
        SetIfMissing(name: "Discord__NotificationsChannelWebhookUrl", value: "http://localhost:1/self-test");
    }

    private static void SetIfMissing(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(variable: name, value: value);
        }
    }

    private static void LoadNativeDependencies()
    {
        // ICU: looking up a specific culture fails when the globalization libraries are missing.
        _ = CultureInfo.GetCultureInfo("en-GB").NativeName;

        // OpenSSL: hashing goes through the platform crypto library.
        _ = SHA256.HashData([1, 2, 3]);
    }
}
