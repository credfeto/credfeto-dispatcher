CREATE INDEX [IX_Issues_Active]
  ON [dbo].[Issues] ([Repository], [Id])
  INCLUDE (
    [Status],
    [FirstSeen],
    [LastUpdated],
    [WhenClosed],
    [Priority],
    [IsOnHold],
    [LinkedPrNumber]
  )
  WHERE [Status] = N'Open';
