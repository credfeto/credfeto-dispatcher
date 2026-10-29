CREATE OR ALTER PROCEDURE [dbo].[WorkItems_GetPrioritised]
  @owners NVARCHAR(MAX),
  @boostedRepos NVARCHAR(MAX),
  @maxIssues INT,
  @user NVARCHAR(100)
AS
BEGIN
  SET NOCOUNT ON;

  DECLARE @ownerList NVARCHAR(MAX) = N',' + ISNULL(@owners, N'') + N',';
  DECLARE @boostedList NVARCHAR(MAX) = N',' + ISNULL(@boostedRepos, N'') + N',';

  -- Eligible issues: same filters as Issues_GetActive
  WITH
    ActiveIssues AS (
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
        LEFT(Iss.[Repository], CHARINDEX(N'/', Iss.[Repository] + N'/') - 1) AS [Owner]
      FROM [dbo].[Issues] AS Iss
      WHERE Iss.[Status] = N'Open'
        AND Iss.[IsOnHold] = 0
        AND NOT EXISTS (
          SELECT 1 FROM [dbo].[Repos] AS Repo
          WHERE Repo.[Repository] = Iss.[Repository] AND Repo.[IsActive] = 0
        )
        AND (
          Iss.[LinkedPrNumber] IS NULL
          OR NOT EXISTS (
            SELECT 1 FROM [dbo].[PullRequests] AS Pr
            WHERE Pr.[Repository] = Iss.[Repository]
              AND Pr.[Id] = Iss.[LinkedPrNumber]
              AND (Pr.[Status] = N'Open' OR Pr.[Status] = N'Draft')
          )
        )
        AND (
          Iss.[Priority] >= 4
          OR NOT EXISTS (
            SELECT 1 FROM [dbo].[PullRequests] AS Pr2
            WHERE Pr2.[Repository] = Iss.[Repository]
              AND (Pr2.[Status] = N'Open' OR Pr2.[Status] = N'Draft')
              AND Pr2.[IsOnHold] = 0
              AND (
                @user IS NULL
                OR Pr2.[IsAdopted] = 1
                OR NOT EXISTS (
                  SELECT 1 FROM [dbo].[PullRequestAssignees] AS PrAssignee
                  WHERE PrAssignee.[Repository] = Pr2.[Repository] AND PrAssignee.[Id] = Pr2.[Id]
                )
                OR EXISTS (
                  SELECT 1 FROM [dbo].[PullRequestAssignees] AS PrAssignee
                  WHERE PrAssignee.[Repository] = Pr2.[Repository]
                    AND PrAssignee.[Id] = Pr2.[Id]
                    AND PrAssignee.[Login] = @user
                )
              )
              AND (
                @user IS NULL
                OR NOT EXISTS (
                  SELECT 1 FROM [dbo].[PullRequestCommitAuthors] AS CommitAuthor
                  WHERE CommitAuthor.[Repository] = Pr2.[Repository] AND CommitAuthor.[Id] = Pr2.[Id]
                )
                OR EXISTS (
                  SELECT 1 FROM [dbo].[PullRequestCommitAuthors] AS CommitAuthor
                  WHERE CommitAuthor.[Repository] = Pr2.[Repository]
                    AND CommitAuthor.[Id] = Pr2.[Id]
                    AND CommitAuthor.[Login] = @user
                )
              )
          )
        )
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
    ),

    -- Only the highest priority (then oldest) issue of each repository is offered
    TopIssues AS (
      SELECT
        ActiveIssues.[Repository],
        ActiveIssues.[Id],
        ActiveIssues.[Status],
        ActiveIssues.[FirstSeen],
        ActiveIssues.[LastUpdated],
        ActiveIssues.[WhenClosed],
        ActiveIssues.[Priority],
        ActiveIssues.[IsOnHold],
        ActiveIssues.[LinkedPrNumber],
        ActiveIssues.[Owner],
        ROW_NUMBER() OVER (
          PARTITION BY ActiveIssues.[Repository]
          ORDER BY ActiveIssues.[Priority] DESC, ActiveIssues.[FirstSeen] ASC
        ) AS [RepoRank]
      FROM ActiveIssues
    ),

    -- The cap applies per owner to issues below Urgent; Urgent and above always pass
    RankedIssues AS (
      SELECT
        TopIssues.[Repository],
        TopIssues.[Id],
        TopIssues.[Status],
        TopIssues.[FirstSeen],
        TopIssues.[LastUpdated],
        TopIssues.[WhenClosed],
        TopIssues.[Priority],
        TopIssues.[IsOnHold],
        TopIssues.[LinkedPrNumber],
        TopIssues.[Owner],
        SUM(CASE WHEN TopIssues.[Priority] < 4 THEN 1 ELSE 0 END) OVER (
          PARTITION BY TopIssues.[Owner]
          ORDER BY TopIssues.[Priority] DESC, TopIssues.[FirstSeen] ASC
          ROWS UNBOUNDED PRECEDING
        ) AS [LowerCount]
      FROM TopIssues
      WHERE TopIssues.[RepoRank] = 1
    ),

    CappedIssues AS (
      SELECT
        RankedIssues.[Repository],
        RankedIssues.[Id],
        RankedIssues.[Status],
        RankedIssues.[FirstSeen],
        RankedIssues.[LastUpdated],
        RankedIssues.[WhenClosed],
        RankedIssues.[Priority],
        RankedIssues.[IsOnHold],
        RankedIssues.[LinkedPrNumber],
        RankedIssues.[Owner]
      FROM RankedIssues
      WHERE RankedIssues.[Priority] >= 4
        OR @maxIssues <= 0
        OR RankedIssues.[LowerCount] <= @maxIssues
    ),

    ActivePullRequests AS (
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
        LEFT(Pr.[Repository], CHARINDEX(N'/', Pr.[Repository] + N'/') - 1) AS [Owner]
      FROM [dbo].[PullRequests] AS Pr
      WHERE (Pr.[Status] = N'Open' OR Pr.[Status] = N'Draft')
        AND Pr.[IsOnHold] = 0
        AND NOT EXISTS (
          SELECT 1 FROM [dbo].[Repos] AS Repo
          WHERE Repo.[Repository] = Pr.[Repository] AND Repo.[IsActive] = 0
        )
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
    ),

    WorkItems AS (
      SELECT
        Pr.[Repository],
        Pr.[Id],
        N'PullRequest'    AS [ItemType],
        Pr.[Status],
        Pr.[FirstSeen],
        Pr.[LastUpdated],
        Pr.[WhenClosed],
        Pr.[Priority],
        Pr.[IsOnHold],
        CAST(NULL AS INT) AS [LinkedPrNumber],
        Pr.[CommentCount],
        Pr.[ReviewDecision],
        Pr.[FailedCheckCount],
        Pr.[FailedCheckNames],
        Pr.[FailedCheckSha],
        Pr.[Author],
        Pr.[Owner],
        0                 AS [TypeOrder]
      FROM ActivePullRequests AS Pr
      UNION ALL
      SELECT
        Iss.[Repository],
        Iss.[Id],
        N'Issue'                    AS [ItemType],
        Iss.[Status],
        Iss.[FirstSeen],
        Iss.[LastUpdated],
        Iss.[WhenClosed],
        Iss.[Priority],
        Iss.[IsOnHold],
        Iss.[LinkedPrNumber],
        0                           AS [CommentCount],
        CAST(NULL AS NVARCHAR(MAX)) AS [ReviewDecision],
        0                           AS [FailedCheckCount],
        CAST(NULL AS NVARCHAR(MAX)) AS [FailedCheckNames],
        CAST(NULL AS NVARCHAR(MAX)) AS [FailedCheckSha],
        CAST(NULL AS NVARCHAR(MAX)) AS [Author],
        Iss.[Owner],
        1                           AS [TypeOrder]
      FROM CappedIssues AS Iss
    )

  SELECT
    Item.[Repository],
    Item.[Id],
    Item.[ItemType],
    Item.[Status],
    Item.[FirstSeen],
    Item.[LastUpdated],
    Item.[WhenClosed],
    Item.[Priority],
    Item.[IsOnHold],
    Item.[LinkedPrNumber],
    Item.[CommentCount],
    Item.[ReviewDecision],
    Item.[FailedCheckCount],
    Item.[FailedCheckNames],
    Item.[FailedCheckSha],
    Item.[Author]
  FROM WorkItems AS Item
  ORDER BY
    Item.[TypeOrder] ASC,
    CASE WHEN Item.[Priority] >= 4 THEN 0 ELSE 1 END,
    CASE WHEN CHARINDEX(N',' + Item.[Repository] + N',', @boostedList) > 0
        THEN CHARINDEX(N',' + Item.[Repository] + N',', @boostedList)
      ELSE 2147483647
    END,
    CASE WHEN CHARINDEX(N',' + Item.[Owner] + N',', @ownerList) > 0
        THEN CHARINDEX(N',' + Item.[Owner] + N',', @ownerList)
      ELSE 2147483647
    END,
    Item.[Owner] ASC,
    Item.[Priority] DESC,
    Item.[FirstSeen] ASC;
END;
GO
