using System;
using Credfeto.Dispatcher.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests.Helpers;

public sealed class StartupBannerTests : TestBase
{
    [Fact]
    public void ShowDoesNotThrow()
    {
        Exception? exception = Record.Exception(StartupBanner.Show);

        Assert.Null(exception);
    }
}
