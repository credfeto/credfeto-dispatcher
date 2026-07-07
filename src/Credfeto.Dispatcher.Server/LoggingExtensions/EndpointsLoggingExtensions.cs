using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dispatcher.Server.LoggingExtensions;

internal static partial class EndpointsLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Unhandled exception in /priorities handler")]
    public static partial void LogUnhandledException(this ILogger logger, Exception exception);
}
