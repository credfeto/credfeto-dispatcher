using System;
using System.Threading;
using Credfeto.Dispatcher.Server.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests.Helpers;

[Collection(ServerEnvironment.NAME)]
public sealed class ServerStartupThreadTests : TestBase
{
    [Fact]
    public void SetThreadsRaisesEachMinimumThatIsBelowTheTarget()
    {
        ThreadPool.GetMinThreads(out int originalWorker, out int originalIoc);
        int target = Math.Max(val1: originalWorker, val2: originalIoc) + 1;

        try
        {
            ServerStartup.SetThreads(target);
            AssertMinThreads(expectedWorker: target, expectedIoc: target);

            ThreadPool.SetMinThreads(workerThreads: originalWorker, completionPortThreads: target + 1);
            ServerStartup.SetThreads(target);
            AssertMinThreads(expectedWorker: target, expectedIoc: target + 1);

            ThreadPool.SetMinThreads(workerThreads: target + 1, completionPortThreads: originalIoc);
            ServerStartup.SetThreads(target);
            AssertMinThreads(expectedWorker: target + 1, expectedIoc: target);

            ServerStartup.SetThreads(1);
            AssertMinThreads(expectedWorker: target + 1, expectedIoc: target);
        }
        finally
        {
            ThreadPool.SetMinThreads(workerThreads: originalWorker, completionPortThreads: originalIoc);
        }
    }

    private static void AssertMinThreads(int expectedWorker, int expectedIoc)
    {
        ThreadPool.GetMinThreads(out int worker, out int ioc);
        Assert.Equal(expected: expectedWorker, actual: worker);
        Assert.Equal(expected: expectedIoc, actual: ioc);
    }
}
