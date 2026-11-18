using Xunit;

namespace Credfeto.Dispatcher.Server.Tests;

// The server reads its configuration from process-wide environment variables, which some tests
// change, so every test that builds the real application must run in this one serial collection.
[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class ServerEnvironment
{
    public const string NAME = "Server environment";
}
