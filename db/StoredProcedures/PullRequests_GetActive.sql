CREATE PROCEDURE [dbo].[PullRequests_GetActive]
  @user NVARCHAR(100)
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
    [IsAdopted]
  FROM [dbo].[PullRequests]
  WHERE ([Status] = N'Open' OR [Status] = N'Draft')
    AND [IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS Repo
      WHERE Repo.[Repository] = [PullRequests].[Repository] AND Repo.[IsActive] = 0
    )
    AND (
      @user IS NULL
      OR [IsAdopted] = 1
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequestAssignees] AS Assignee
        WHERE Assignee.[Repository] = [PullRequests].[Repository] AND Assignee.[Id] = [PullRequests].[Id]
      )
      OR EXISTS (
        SELECT 1 FROM [dbo].[PullRequestAssignees] AS Assignee
        WHERE Assignee.[Repository] = [PullRequests].[Repository]
          AND Assignee.[Id] = [PullRequests].[Id]
          AND Assignee.[Login] = @user
      )
    );
END;
