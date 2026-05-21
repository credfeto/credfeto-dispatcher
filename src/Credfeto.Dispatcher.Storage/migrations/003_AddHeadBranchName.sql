IF
  NOT EXISTS (
    SELECT 1 FROM [sys].[columns]
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PullRequests]')
      AND [name] = N'HeadBranchName'
  )
  BEGIN
    ALTER TABLE [dbo].[PullRequests]
    ADD [HeadBranchName] NVARCHAR(MAX) NULL;
  END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_GetActive]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(MAX),
  @priority INT,
  @isOnHold BIT,
  @commentCount INT,
  @reviewDecision NVARCHAR(MAX),
  @failedCheckCount INT,
  @failedCheckNames NVARCHAR(MAX),
  @failedCheckSha NVARCHAR(MAX),
  @author NVARCHAR(MAX),
  @headBranchName NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  MERGE [dbo].[PullRequests] AS [Target]
  USING (
    SELECT
      @repository AS [Repository],
      @id         AS [Id]
  ) AS [Source]
  ON [Target].[Repository] = [Source].[Repository] AND [Target].[Id] = [Source].[Id]
  WHEN MATCHED
    THEN
    UPDATE
      SET
        [Status] = @status,
        [Priority] = @priority,
        [IsOnHold] = @isOnHold,
        [CommentCount] = @commentCount,
        [ReviewDecision] = @reviewDecision,
        [FailedCheckCount] = @failedCheckCount,
        [FailedCheckNames] = @failedCheckNames,
        [FailedCheckSha] = @failedCheckSha,
        [Author] = ISNULL(@author, [Target].[Author]),
        [HeadBranchName] = ISNULL(@headBranchName, [Target].[HeadBranchName]),
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [CommentCount],
      [ReviewDecision], [FailedCheckCount], [FailedCheckNames], [FailedCheckSha],
      [Author], [HeadBranchName], [FirstSeen], [LastUpdated], [WhenClosed]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @commentCount,
      @reviewDecision, @failedCheckCount, @failedCheckNames, @failedCheckSha,
      @author, @headBranchName, @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END
    );
END;
GO
