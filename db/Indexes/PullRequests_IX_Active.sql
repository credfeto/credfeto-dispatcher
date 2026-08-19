CREATE INDEX [IX_PullRequests_Active]
  ON [dbo].[PullRequests] ([Repository], [Id])
  INCLUDE (
    [Status],
    [FirstSeen],
    [LastUpdated],
    [WhenClosed],
    [Priority],
    [IsOnHold],
    [CommentCount],
    [ReviewDecision],
    [FailedCheckCount],
    [FailedCheckNames],
    [FailedCheckSha],
    [Author]
  )
  WHERE [Status] IN (N'Open', N'Draft');
