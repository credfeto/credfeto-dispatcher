using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{WorkflowRuns.Count} runs")]
internal sealed record ApiWorkflowRunsResponse(
    [property: JsonPropertyName("workflow_runs")] IReadOnlyList<ApiWorkflowRun> WorkflowRuns
);
