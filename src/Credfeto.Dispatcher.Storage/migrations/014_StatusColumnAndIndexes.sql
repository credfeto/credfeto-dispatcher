ALTER TABLE [dbo].[PullRequests]
ALTER COLUMN [Status] NVARCHAR(16) NOT NULL;
GO

ALTER TABLE [dbo].[Issues]
ALTER COLUMN [Status] NVARCHAR(16) NOT NULL;
GO

-- Filtered indexes require QUOTED_IDENTIFIER ON; sqlcmd connections default it to OFF.
SET QUOTED_IDENTIFIER ON;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = N'IX_PullRequests_Active' AND [object_id] = OBJECT_ID(N'[dbo].[PullRequests]')
  )
  BEGIN
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
  END;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = N'IX_Issues_Active' AND [object_id] = OBJECT_ID(N'[dbo].[Issues]')
  )
  BEGIN
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
  END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(16),
  @priority INT,
  @isOnHold BIT,
  @hasDetail BIT,
  @commentCount INT,
  @reviewDecision NVARCHAR(MAX),
  @failedCheckCount INT,
  @failedCheckNames NVARCHAR(MAX),
  @failedCheckSha NVARCHAR(MAX),
  @author NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[PullRequests] WITH (HOLDLOCK) AS [Target]
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
        [CommentCount] = CASE WHEN @hasDetail = 1 THEN @commentCount ELSE [Target].[CommentCount] END,
        [ReviewDecision] = CASE WHEN @hasDetail = 1 THEN @reviewDecision ELSE [Target].[ReviewDecision] END,
        [FailedCheckCount] = CASE WHEN @hasDetail = 1 THEN @failedCheckCount ELSE [Target].[FailedCheckCount] END,
        [FailedCheckNames] = CASE WHEN @hasDetail = 1 THEN @failedCheckNames ELSE [Target].[FailedCheckNames] END,
        [FailedCheckSha] = CASE WHEN @hasDetail = 1 THEN @failedCheckSha ELSE [Target].[FailedCheckSha] END,
        [Author] = ISNULL(@author, [Target].[Author]),
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END,
        [DateStatusChanged] = CASE WHEN [Target].[Status] <> @status THEN @now ELSE [Target].[DateStatusChanged] END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [CommentCount],
      [ReviewDecision], [FailedCheckCount], [FailedCheckNames], [FailedCheckSha],
      [Author], [FirstSeen], [LastUpdated], [WhenClosed], [DateStatusChanged]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @commentCount,
      @reviewDecision, @failedCheckCount, @failedCheckNames, @failedCheckSha,
      @author, @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END,
      @now
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(16),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[Issues] WITH (HOLDLOCK) AS [Target]
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
        [LinkedPrNumber] = ISNULL(@linkedPrNumber, [Target].[LinkedPrNumber]),
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END,
        [DateStatusChanged] = CASE WHEN [Target].[Status] <> @status THEN @now ELSE [Target].[DateStatusChanged] END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [LinkedPrNumber],
      [FirstSeen], [LastUpdated], [WhenClosed], [DateStatusChanged]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @linkedPrNumber,
      @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END,
      @now
    );
END;
GO
