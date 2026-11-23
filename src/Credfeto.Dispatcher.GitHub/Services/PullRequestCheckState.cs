using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Services;

// What the CI refresh knows about one open PR. RunSignature and ETag describe the workflow runs for HeadSha as last
// read; Settled means every run completed and none failed, so the PR needs no further CI polling until it changes.
[DebuggerDisplay("{Repo}#{Number}: Sha={HeadSha}, Settled={Settled}, OnHold={IsOnHold}, LastChecked={LastChecked}")]
public sealed record PullRequestCheckState(
    string Repo,
    int Number,
    string? HeadSha,
    string? ETag,
    string? RunSignature,
    bool Settled,
    bool IsOnHold,
    DateTimeOffset LastChecked
);
