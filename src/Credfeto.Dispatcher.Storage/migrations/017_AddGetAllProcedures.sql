CREATE OR ALTER PROCEDURE [dbo].[PullRequests_GetAll]
  @owner NVARCHAR(100),
  @repo NVARCHAR(100),
  @user NVARCHAR(100)
AS
BEGIN
  SET NOCOUNT ON;
  -- Repository is always 'owner/repo'; owner logins cannot contain LIKE wildcards, repo names can contain '_'.
  DECLARE
    @repositoryPattern NVARCHAR(450)
    = CASE
      WHEN @owner IS NULL AND @repo IS NULL THEN N'%'
      WHEN @repo IS NULL THEN @owner + N'/%'
      WHEN @owner IS NULL THEN N'%/' + REPLACE(@repo, N'_', N'\_')
      ELSE @owner + N'/' + REPLACE(@repo, N'_', N'\_')
    END;
  SELECT
    Pr.[Repository],
    Pr.[Id],
    Pr.[Status],
    Pr.[FirstSeen],
    Pr.[LastUpdated],
    Pr.[WhenClosed],
    Pr.[Priority],
    Pr.[IsOnHold],
    Pr.[CommentCount],
    Pr.[ReviewDecision],
    Pr.[FailedCheckCount],
    Pr.[FailedCheckNames],
    Pr.[FailedCheckSha],
    Pr.[Author],
    Pr.[IsAdopted],
    (
      SELECT STRING_AGG(Assignee.[Login], N',') WITHIN GROUP (ORDER BY Assignee.[Login])
      FROM [dbo].[PullRequestAssignees] AS Assignee
      WHERE Assignee.[Repository] = Pr.[Repository] AND Assignee.[Id] = Pr.[Id]
    )                                   AS [Assignees]
  FROM [dbo].[PullRequests] AS Pr
  WHERE Pr.[Repository] LIKE @repositoryPattern ESCAPE N'\'
    AND (
      @user IS NULL
      OR Pr.[IsAdopted] = 1
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequestAssignees] AS Assignee
        WHERE Assignee.[Repository] = Pr.[Repository] AND Assignee.[Id] = Pr.[Id]
      )
      OR EXISTS (
        SELECT 1 FROM [dbo].[PullRequestAssignees] AS Assignee
        WHERE Assignee.[Repository] = Pr.[Repository]
          AND Assignee.[Id] = Pr.[Id]
          AND Assignee.[Login] = @user
      )
    )
  ORDER BY Pr.[Repository], Pr.[Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_GetAll]
  @owner NVARCHAR(100),
  @repo NVARCHAR(100),
  @user NVARCHAR(100)
AS
BEGIN
  SET NOCOUNT ON;
  -- Repository is always 'owner/repo'; owner logins cannot contain LIKE wildcards, repo names can contain '_'.
  DECLARE
    @repositoryPattern NVARCHAR(450)
    = CASE
      WHEN @owner IS NULL AND @repo IS NULL THEN N'%'
      WHEN @repo IS NULL THEN @owner + N'/%'
      WHEN @owner IS NULL THEN N'%/' + REPLACE(@repo, N'_', N'\_')
      ELSE @owner + N'/' + REPLACE(@repo, N'_', N'\_')
    END;
  SELECT
    Iss.[Repository],
    Iss.[Id],
    Iss.[Status],
    Iss.[FirstSeen],
    Iss.[LastUpdated],
    Iss.[WhenClosed],
    Iss.[Priority],
    Iss.[IsOnHold],
    Iss.[LinkedPrNumber],
    (
      SELECT STRING_AGG(Assignee.[Login], N',') WITHIN GROUP (ORDER BY Assignee.[Login])
      FROM [dbo].[IssueAssignees] AS Assignee
      WHERE Assignee.[Repository] = Iss.[Repository] AND Assignee.[Id] = Iss.[Id]
    )                             AS [Assignees]
  FROM [dbo].[Issues] AS Iss
  WHERE Iss.[Repository] LIKE @repositoryPattern ESCAPE N'\'
    AND (
      @user IS NULL
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[IssueAssignees] AS Assignee
        WHERE Assignee.[Repository] = Iss.[Repository] AND Assignee.[Id] = Iss.[Id]
      )
      OR EXISTS (
        SELECT 1 FROM [dbo].[IssueAssignees] AS Assignee
        WHERE Assignee.[Repository] = Iss.[Repository]
          AND Assignee.[Id] = Iss.[Id]
          AND Assignee.[Login] = @user
      )
    )
  ORDER BY Iss.[Repository], Iss.[Id];
END;
GO
