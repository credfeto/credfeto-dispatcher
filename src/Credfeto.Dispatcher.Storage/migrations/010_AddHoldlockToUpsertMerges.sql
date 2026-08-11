CREATE OR ALTER PROCEDURE [dbo].[PollingStates_Upsert]
  @key NVARCHAR(256),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[PollingStates] WITH (HOLDLOCK) AS [Target]
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

CREATE OR ALTER PROCEDURE [dbo].[Issues_Upsert]
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
