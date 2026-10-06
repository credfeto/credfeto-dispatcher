using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;

namespace Credfeto.Dispatcher.GitHub.Tests.Helpers;

internal static class LoggerSubstituteExtensions
{
    // Source-generated [LoggerMessage] methods return before calling Log when IsEnabled is false,
    // which is the substitute's default, so nothing would be recorded without this.
    public static void MockLoggerIsEnabled(this ILogger logger)
    {
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    public static IReadOnlyList<Exception?> LoggedErrors(this ILogger logger)
    {
        return [.. logger.ReceivedCalls().Where(IsErrorLogCall).Select(call => call.GetArguments()[3] as Exception)];
    }

    private static bool IsErrorLogCall(ICall call)
    {
        return StringComparer.Ordinal.Equals(x: call.GetMethodInfo().Name, y: nameof(ILogger.Log))
            && call.GetArguments()[0] is LogLevel.Error;
    }
}
