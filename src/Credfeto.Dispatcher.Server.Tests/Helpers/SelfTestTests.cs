using Credfeto.Dispatcher.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests.Helpers;

public sealed class SelfTestTests : TestBase
{
    [Fact]
    public void IsSelfTestWhenSwitchPresent()
    {
        Assert.True(SelfTest.IsSelfTest(["--urls", SelfTest.SWITCH]), userMessage: "Expected self-test");
    }

    [Fact]
    public void IsNotSelfTestWhenSwitchAbsent()
    {
        Assert.False(SelfTest.IsSelfTest(["--urls"]), userMessage: "Expected no self-test");
    }

    [Fact]
    public void WithoutSwitchRemovesOnlyTheSwitch()
    {
        string[] result = SelfTest.WithoutSwitch(["--first", SelfTest.SWITCH, "--second"]);

        Assert.Equal(expected: ["--first", "--second"], actual: result);
    }
}
