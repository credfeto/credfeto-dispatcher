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
    [Author],
    [HeadBranchName]
  FROM [dbo].[PullRequests]
  WHERE [Status] <> N'Closed'
    AND [IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS R
      WHERE R.[Repository] = [PullRequests].[Repository] AND R.[IsActive] = 0
    );
END;
