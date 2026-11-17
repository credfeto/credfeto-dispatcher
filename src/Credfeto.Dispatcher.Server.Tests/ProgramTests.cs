using System.Threading.Tasks;
using Credfeto.Dispatcher.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests;

[Collection(ServerEnvironment.NAME)]
public sealed class ProgramTests : TestBase
{
    private const string DATABASE_PROVIDER_VARIABLE = "DatabaseConfiguration__Provider";

    [Fact]
    public async Task MainWithSelfTestSwitchSucceedsAsync()
    {
        int exitCode = await Program.Main([SelfTest.SWITCH]);

        Assert.Equal(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task MainWithHealthCheckAgainstAnUnreachableServerFailsAsync()
    {
        int exitCode = await Program.Main(["--health-check", "http://127.0.0.1:1/ping"]);

        Assert.NotEqual(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task MainWhenTheServerCannotBeCreatedFailsAsync()
    {
        using EnvironmentOverride provider = new(name: DATABASE_PROVIDER_VARIABLE, value: "Unsupported");

        int exitCode = await Program.Main([]);

        Assert.Equal(expected: 1, actual: exitCode);
    }

    [Fact]
    public async Task SelfTestWhenTheServerCannotBeCreatedFailsAsync()
    {
        using EnvironmentOverride provider = new(name: DATABASE_PROVIDER_VARIABLE, value: "Unsupported");

        int exitCode = await SelfTest.RunAsync([SelfTest.SWITCH]);

        Assert.Equal(expected: 1, actual: exitCode);
    }
}
