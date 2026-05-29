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
    [Author]
  FROM [dbo].[PullRequests]
  WHERE [Status] <> N'Closed'
    AND [IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS R
      WHERE R.[Repository] = [PullRequests].[Repository] AND R.[IsActive] = 0
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_GetActive]
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    I.[Repository],
    I.[Id],
    I.[Status],
    I.[FirstSeen],
    I.[LastUpdated],
    I.[WhenClosed],
    I.[Priority],
    I.[IsOnHold],
    I.[LinkedPrNumber]
  FROM [dbo].[Issues] AS I
  WHERE I.[Status] <> N'Closed'
    AND I.[IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS R
      WHERE R.[Repository] = I.[Repository] AND R.[IsActive] = 0
    )
    AND (
      I.[LinkedPrNumber] IS NULL
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequests] AS Pr
        WHERE Pr.[Repository] = I.[Repository]
          AND Pr.[Id] = I.[LinkedPrNumber]
          AND Pr.[Status] <> N'Closed'
      )
    )
    AND (
      I.[Priority] >= 4
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequests] AS Pr2
        WHERE Pr2.[Repository] = I.[Repository] AND Pr2.[Status] <> N'Closed'
      )
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Repos_SetActive]
  @repositories NVARCHAR(MAX),
  @lastUpdated DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @ActiveRepos TABLE ([Repository] NVARCHAR(450) NOT NULL);
  IF @repositories IS NOT NULL
    BEGIN
      INSERT INTO @ActiveRepos ([Repository])
      SELECT [Source].[Repository]
      FROM (
        SELECT TRIM([value]) AS [Repository]
        FROM STRING_SPLIT(@repositories, N',')
      ) AS [Source]
      WHERE [Source].[Repository] <> N'';
    END;
  MERGE [dbo].[Repos] AS [Target]
  USING (
    SELECT
      [Repository],
      1 AS [IsActive]
    FROM @ActiveRepos
  ) AS [Source]
  ON [Target].[Repository] = [Source].[Repository]
  WHEN MATCHED
    THEN
    UPDATE SET [IsActive] = 1, [LastUpdated] = @lastUpdated
  WHEN NOT MATCHED BY TARGET
    THEN
    INSERT ([Repository], [IsActive], [LastUpdated])
    VALUES ([Source].[Repository], 1, @lastUpdated)
  WHEN NOT MATCHED BY SOURCE
    THEN
    UPDATE SET [IsActive] = 0, [LastUpdated] = @lastUpdated;
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
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [CommentCount],
      [ReviewDecision], [FailedCheckCount], [FailedCheckNames], [FailedCheckSha],
      [Author], [FirstSeen], [LastUpdated], [WhenClosed]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @commentCount,
      @reviewDecision, @failedCheckCount, @failedCheckNames, @failedCheckSha,
      @author, @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(MAX),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT,
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
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
        [LinkedPrNumber] = @linkedPrNumber,
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [LinkedPrNumber],
      [FirstSeen], [LastUpdated], [WhenClosed]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @linkedPrNumber,
      @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[NotificationQueue_Upsert]
  @subjectUrl NVARCHAR(450),
  @notificationId NVARCHAR(MAX),
  @repository NVARCHAR(450),
  @repositoryUrl NVARCHAR(450),
  @subjectType NVARCHAR(MAX),
  @subjectTitle NVARCHAR(MAX),
  @reason NVARCHAR(MAX),
  @updatedAt DATETIMEOFFSET,
  @queuedAt DATETIMEOFFSET,
  @dispatchAfter DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  MERGE [dbo].[NotificationQueue] AS [Target]
  USING (SELECT @subjectUrl AS [SubjectUrl]) AS [Source] ON [Target].[SubjectUrl] = [Source].[SubjectUrl]
  WHEN MATCHED
    THEN
    UPDATE
      SET
        [NotificationId] = @notificationId,
        [Reason] = @reason,
        [SubjectTitle] = @subjectTitle,
        [UpdatedAt] = @updatedAt,
        [QueuedAt] = @queuedAt,
        [DispatchAfter] = @dispatchAfter
  WHEN NOT MATCHED
    THEN
    INSERT (
      [SubjectUrl], [NotificationId], [Repository], [RepositoryUrl], [SubjectType],
      [SubjectTitle], [Reason], [UpdatedAt], [QueuedAt], [DispatchAfter]
    )
    VALUES (
      @subjectUrl, @notificationId, @repository, @repositoryUrl, @subjectType,
      @subjectTitle, @reason, @updatedAt, @queuedAt, @dispatchAfter
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[NotificationQueue_Delete]
  @subjectUrl NVARCHAR(450)
AS
BEGIN
  SET NOCOUNT ON;
  DELETE FROM [dbo].[NotificationQueue]
  WHERE [SubjectUrl] = @subjectUrl;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[NotificationQueue_GetReady]
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    [SubjectUrl],
    [NotificationId],
    [Repository],
    [RepositoryUrl],
    [SubjectType],
    [SubjectTitle],
    [Reason],
    [UpdatedAt],
    [QueuedAt],
    [DispatchAfter]
  FROM [dbo].[NotificationQueue]
  WHERE [DispatchAfter] <= @now
  ORDER BY [QueuedAt];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PollingStates_GetByKey]
  @key NVARCHAR(256)
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    [Key],
    [ETag]
  FROM [dbo].[PollingStates]
  WHERE [Key] = @key;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PollingStates_Upsert]
  @key NVARCHAR(256),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  MERGE [dbo].[PollingStates] AS [Target]
  USING (SELECT @key AS [Key]) AS [Source] ON [Target].[Key] = [Source].[Key]
  WHEN MATCHED THEN UPDATE SET [ETag] = @eTag
  WHEN NOT MATCHED THEN INSERT ([Key], [ETag]) VALUES (@key, @eTag);
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_RemoveForRepositories]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @Repositories TABLE ([Repository] NVARCHAR(450) NOT NULL PRIMARY KEY);
  IF @repositories IS NULL
    BEGIN
      RETURN;
    END;
  INSERT INTO @Repositories ([Repository])
  SELECT [Source].[Repository]
  FROM (
    SELECT TRIM([value]) AS [Repository]
    FROM STRING_SPLIT(@repositories, N',')
  ) AS [Source]
  WHERE [Source].[Repository] <> N'';
  DELETE FROM [dbo].[PullRequests]
  WHERE EXISTS (
      SELECT 1
      FROM @Repositories AS [Source]
      WHERE [Source].[Repository] = [dbo].[PullRequests].[Repository]
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_RemoveForRepositories]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @Repositories TABLE ([Repository] NVARCHAR(450) NOT NULL PRIMARY KEY);
  IF @repositories IS NULL
    BEGIN
      RETURN;
    END;
  INSERT INTO @Repositories ([Repository])
  SELECT [Source].[Repository]
  FROM (
    SELECT TRIM([value]) AS [Repository]
    FROM STRING_SPLIT(@repositories, N',')
  ) AS [Source]
  WHERE [Source].[Repository] <> N'';
  DELETE FROM [dbo].[Issues]
  WHERE EXISTS (
      SELECT 1
      FROM @Repositories AS [Source]
      WHERE [Source].[Repository] = [dbo].[Issues].[Repository]
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_CloseStale]
  @repository NVARCHAR(450),
  @activePrIds NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @ActivePullRequests TABLE ([Id] INT NOT NULL PRIMARY KEY);
  IF @activePrIds IS NOT NULL
    BEGIN
      INSERT INTO @ActivePullRequests ([Id])
      SELECT [Source].[Id]
      FROM (
        SELECT TRY_CAST(TRIM([value]) AS INT) AS [Id]
        FROM STRING_SPLIT(@activePrIds, N',')
      ) AS [Source]
      WHERE [Source].[Id] IS NOT NULL;
    END;
  UPDATE [dbo].[PullRequests]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Status] <> N'Closed'
    AND NOT EXISTS (
      SELECT 1
      FROM @ActivePullRequests AS [Source]
      WHERE [Source].[Id] = [dbo].[PullRequests].[Id]
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_CloseStale]
  @repository NVARCHAR(450),
  @activeIssueIds NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @ActiveIssues TABLE ([Id] INT NOT NULL PRIMARY KEY);
  IF @activeIssueIds IS NOT NULL
    BEGIN
      INSERT INTO @ActiveIssues ([Id])
      SELECT [Source].[Id]
      FROM (
        SELECT TRY_CAST(TRIM([value]) AS INT) AS [Id]
        FROM STRING_SPLIT(@activeIssueIds, N',')
      ) AS [Source]
      WHERE [Source].[Id] IS NOT NULL;
    END;
  UPDATE [dbo].[Issues]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Status] <> N'Closed'
    AND NOT EXISTS (
      SELECT 1
      FROM @ActiveIssues AS [Source]
      WHERE [Source].[Id] = [dbo].[Issues].[Id]
    );
END;
GO
