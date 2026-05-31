IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[columns]
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PollingStates]') AND [name] = N'DateCreated'
  )
  BEGIN
    ALTER TABLE [dbo].[PollingStates]
    ADD [DateCreated] DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE();
  END;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[columns]
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PollingStates]') AND [name] = N'DateUpdated'
  )
  BEGIN
    ALTER TABLE [dbo].[PollingStates]
    ADD [DateUpdated] DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE();
  END;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[columns]
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PollingStates]') AND [name] = N'DateStateChanged'
  )
  BEGIN
    ALTER TABLE [dbo].[PollingStates]
    ADD [DateStateChanged] DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE();
  END;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[columns]
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PullRequests]') AND [name] = N'DateStatusChanged'
  )
  BEGIN
    ALTER TABLE [dbo].[PullRequests]
    ADD [DateStatusChanged] DATETIMEOFFSET NULL;
  END;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[columns]
    WHERE [object_id] = OBJECT_ID(N'[dbo].[Issues]') AND [name] = N'DateStatusChanged'
  )
  BEGIN
    ALTER TABLE [dbo].[Issues]
    ADD [DateStatusChanged] DATETIMEOFFSET NULL;
  END;
GO

IF OBJECT_ID(N'[dbo].[PollingStates_Upsert]', N'P') IS NOT NULL
  DROP PROCEDURE [dbo].[PollingStates_Upsert];
GO

CREATE PROCEDURE [dbo].[PollingStates_Upsert]
  @key NVARCHAR(256),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[PollingStates] AS [Target]
  USING (SELECT @key AS [Key]) AS [Source] ON [Target].[Key] = [Source].[Key]
  WHEN MATCHED
    THEN UPDATE SET
      [ETag] = @eTag,
      [DateUpdated] = @now,
      [DateStateChanged] = CASE WHEN [Target].[ETag] <> @eTag THEN @now ELSE [Target].[DateStateChanged] END
  WHEN NOT MATCHED
    THEN INSERT ([Key], [ETag], [DateCreated], [DateUpdated], [DateStateChanged])
    VALUES (@key, @eTag, @now, @now, @now);
END;
GO

IF OBJECT_ID(N'[dbo].[PullRequests_Upsert]', N'P') IS NOT NULL
  DROP PROCEDURE [dbo].[PullRequests_Upsert];
GO

CREATE PROCEDURE [dbo].[PullRequests_Upsert]
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
  @author NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
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

IF OBJECT_ID(N'[dbo].[PullRequests_CloseStale]', N'P') IS NOT NULL
  DROP PROCEDURE [dbo].[PullRequests_CloseStale];
GO

CREATE PROCEDURE [dbo].[PullRequests_CloseStale]
  @repository NVARCHAR(450),
  @activePrIds NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  WITH
    [ActiveIds] AS (
      SELECT TRIM([value]) AS [CleanValue]
      FROM STRING_SPLIT(@activePrIds, N',')
    )

  UPDATE [dbo].[PullRequests]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now, [DateStatusChanged] = @now
  WHERE [Repository] = @repository
    AND ([Status] = N'Open' OR [Status] = N'Draft')
    AND (
      @activePrIds IS NULL
      OR NOT EXISTS (
        SELECT 1
        FROM [ActiveIds]
        WHERE TRY_CAST([ActiveIds].[CleanValue] AS INT) = [dbo].[PullRequests].[Id]
          AND [ActiveIds].[CleanValue] > N''
      )
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Issues_Upsert]', N'P') IS NOT NULL
  DROP PROCEDURE [dbo].[Issues_Upsert];
GO

CREATE PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(MAX),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[Issues] AS [Target]
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

IF OBJECT_ID(N'[dbo].[Issues_CloseStale]', N'P') IS NOT NULL
  DROP PROCEDURE [dbo].[Issues_CloseStale];
GO

CREATE PROCEDURE [dbo].[Issues_CloseStale]
  @repository NVARCHAR(450),
  @activeIssueIds NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  WITH
    [ActiveIds] AS (
      SELECT TRIM([value]) AS [CleanValue]
      FROM STRING_SPLIT(@activeIssueIds, N',')
    )

  UPDATE [dbo].[Issues]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now, [DateStatusChanged] = @now
  WHERE [Repository] = @repository
    AND [Status] = N'Open'
    AND (
      @activeIssueIds IS NULL
      OR NOT EXISTS (
        SELECT 1
        FROM [ActiveIds]
        WHERE TRY_CAST([ActiveIds].[CleanValue] AS INT) = [dbo].[Issues].[Id]
          AND [ActiveIds].[CleanValue] > N''
      )
    );
END;
GO
