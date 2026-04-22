using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Credfeto.Date;
using Credfeto.Dispatcher.Discord;
using Credfeto.Dispatcher.Discord.Configuration;
using Credfeto.Dispatcher.GitHub;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Random;
using Credfeto.Services.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;

namespace Credfeto.Dispatcher.Server.Helpers;

internal static class ServerStartup
{
    public static void SetThreads(int minThreads)
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIoc);
        Console.WriteLine($"Min worker threads {minWorker}, Min IOC threads {minIoc}");

        if (minWorker < minThreads && minIoc < minThreads)
        {
            Console.WriteLine($"Setting min worker threads {minThreads}, Min IOC threads {minThreads}");
            ThreadPool.SetMinThreads(workerThreads: minThreads, completionPortThreads: minThreads);
        }
        else if (minWorker < minThreads)
        {
            Console.WriteLine($"Setting min worker threads {minThreads}, Min IOC threads {minIoc}");
            ThreadPool.SetMinThreads(workerThreads: minThreads, completionPortThreads: minIoc);
        }
        else if (minIoc < minThreads)
        {
            Console.WriteLine($"Setting min worker threads {minWorker}, Min IOC threads {minThreads}");
            ThreadPool.SetMinThreads(workerThreads: minWorker, completionPortThreads: minThreads);
        }

        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIoc);
        Console.WriteLine($"Max worker threads {maxWorker}, Max IOC threads {maxIoc}");
    }

    public static IHost CreateApp(string[] args)
    {
        string configPath = ApplicationConfigLocator.ConfigurationFilesPath;

        return Host.CreateApplicationBuilder(args)
            .ConfigureSettings(configPath)
            .ConfigureServices()
            .ConfigureAppHost()
            .ConfigureLogging()
            .Build();
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2000:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    private static HostApplicationBuilder ConfigureLogging(this HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders().AddSerilog(CreateLogger(), dispose: true);

        return builder;
    }

    private static HostApplicationBuilder ConfigureSettings(this HostApplicationBuilder builder, string configPath)
    {
        builder.Configuration.Sources.Clear();
        builder
            .Configuration.SetBasePath(configPath)
            .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(path: "appsettings-local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        return builder;
    }

    private static HostApplicationBuilder ConfigureServices(this HostApplicationBuilder builder)
    {
        IConfigurationSection gitHubSection = builder.Configuration.GetSection("GitHub");
        IConfigurationSection discordSection = builder.Configuration.GetSection("Discord");

        builder
            .Services.Configure<GitHubOptions>(gitHubSection)
            .Configure<DiscordOptions>(discordSection)
            .AddMediator()
            .AddDate()
            .AddRandomNumbers()
            .AddRunOnStartupServices()
            .AddGitHub()
            .AddDiscord();

        return builder;
    }

    private static HostApplicationBuilder ConfigureAppHost(this HostApplicationBuilder builder)
    {
        return builder;
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration()
            .Enrich.WithDemystifiedStackTraces()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty(name: "ServerVersion", value: VersionInformation.Version)
            .Enrich.WithProperty(name: "ProcessName", value: VersionInformation.Product)
            .WriteToDebuggerAwareOutput()
            .CreateLogger();
    }

    private static LoggerConfiguration WriteToDebuggerAwareOutput(this LoggerConfiguration configuration)
    {
        LoggerSinkConfiguration writeTo = configuration.WriteTo;

        return Debugger.IsAttached ? writeTo.Debug() : writeTo.Console();
    }
}
