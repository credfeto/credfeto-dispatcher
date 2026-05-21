CREATE PROCEDURE [dbo].[PullRequests_GetActive]
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    [Repository],
    [Id],
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
  FROM [dbo].[PullRequests]
  WHERE [Status] IN (N'Open', N'Draft')
    AND [IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS Repo
      WHERE Repo.[Repository] = [PullRequests].[Repository] AND Repo.[IsActive] = 0
    );
END;
