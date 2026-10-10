IF OBJECT_ID(N'[dbo].[PullRequestAssignees]', N'U') IS NULL
  BEGIN
    CREATE TABLE [dbo].[PullRequestAssignees] (
      [Repository] NVARCHAR(450) NOT NULL,
      [Id] INT NOT NULL,
      [Login] NVARCHAR(100) NOT NULL,
      CONSTRAINT [PK_PullRequestAssignees] PRIMARY KEY NONCLUSTERED ([Repository], [Id], [Login]),
      INDEX [IX_PullRequestAssignees_WorkItem] CLUSTERED ([Repository], [Id])
    );
  END;
GO

IF OBJECT_ID(N'[dbo].[IssueAssignees]', N'U') IS NULL
  BEGIN
    CREATE TABLE [dbo].[IssueAssignees] (
      [Repository] NVARCHAR(450) NOT NULL,
      [Id] INT NOT NULL,
      [Login] NVARCHAR(100) NOT NULL,
      CONSTRAINT [PK_IssueAssignees] PRIMARY KEY NONCLUSTERED ([Repository], [Id], [Login]),
      INDEX [IX_IssueAssignees_WorkItem] CLUSTERED ([Repository], [Id])
    );
  END;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[columns]
    WHERE [name] = N'IsAdopted' AND [object_id] = OBJECT_ID(N'[dbo].[PullRequests]')
  )
  BEGIN
    ALTER TABLE [dbo].[PullRequests]
    ADD [IsAdopted] BIT NOT NULL DEFAULT 0;
  END;
GO

-- Filtered indexes require QUOTED_IDENTIFIER ON; sqlcmd connections default it to OFF.
SET QUOTED_IDENTIFIER ON;
GO

IF
  NOT EXISTS (
    SELECT 1
    FROM [sys].[index_columns] AS [IndexColumn]
    INNER JOIN [sys].[indexes] AS [Idx]
    ON [IndexColumn].[object_id] = [Idx].[object_id] AND [IndexColumn].[index_id] = [Idx].[index_id]
    INNER JOIN [sys].[columns] AS [Col]
    ON [IndexColumn].[object_id] = [Col].[object_id] AND [IndexColumn].[column_id] = [Col].[column_id]
    WHERE [Idx].[name] = N'IX_PullRequests_Active'
      AND [Idx].[object_id] = OBJECT_ID(N'[dbo].[PullRequests]')
      AND [Col].[name] = N'IsAdopted'
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
        [Author],
        [IsAdopted]
      )
      WHERE [Status] IN (N'Open', N'Draft')
      WITH (DROP_EXISTING = ON);
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
  @author NVARCHAR(MAX),
  @isAdopted BIT,
  @assignees NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  BEGIN TRANSACTION;
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
        [IsAdopted] = ISNULL(@isAdopted, [Target].[IsAdopted]),
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END,
        [DateStatusChanged] = CASE WHEN [Target].[Status] <> @status THEN @now ELSE [Target].[DateStatusChanged] END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [CommentCount],
      [ReviewDecision], [FailedCheckCount], [FailedCheckNames], [FailedCheckSha],
      [Author], [IsAdopted], [FirstSeen], [LastUpdated], [WhenClosed], [DateStatusChanged]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @commentCount,
      @reviewDecision, @failedCheckCount, @failedCheckNames, @failedCheckSha,
      @author, CASE WHEN @isAdopted IS NULL THEN CAST(0 AS BIT) ELSE @isAdopted END, @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END,
      @now
    );
  IF @assignees IS NOT NULL
    BEGIN
      DELETE FROM [dbo].[PullRequestAssignees] WITH (HOLDLOCK)
      WHERE [Repository] = @repository AND [Id] = @id;
      INSERT INTO [dbo].[PullRequestAssignees] ([Repository], [Id], [Login])
      SELECT DISTINCT
        @repository AS [Repository],
        @id         AS [Id],
        [Source].[Login]
      FROM (
        SELECT TRIM([value]) AS [Login]
        FROM STRING_SPLIT(@assignees, N',')
      ) AS [Source]
      WHERE [Source].[Login] > N'';
    END;
  COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(16),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT,
  @assignees NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  BEGIN TRANSACTION;
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
  IF @assignees IS NOT NULL
    BEGIN
      DELETE FROM [dbo].[IssueAssignees] WITH (HOLDLOCK)
      WHERE [Repository] = @repository AND [Id] = @id;
      INSERT INTO [dbo].[IssueAssignees] ([Repository], [Id], [Login])
      SELECT DISTINCT
        @repository AS [Repository],
        @id         AS [Id],
        [Source].[Login]
      FROM (
        SELECT TRIM([value]) AS [Login]
        FROM STRING_SPLIT(@assignees, N',')
      ) AS [Source]
      WHERE [Source].[Login] > N'';
    END;
  COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_GetActive]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_GetActive]
  @user NVARCHAR(100)
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    Iss.[Repository],
    Iss.[Id],
    Iss.[Status],
    Iss.[FirstSeen],
    Iss.[LastUpdated],
    Iss.[WhenClosed],
    Iss.[Priority],
    Iss.[IsOnHold],
    Iss.[LinkedPrNumber]
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
        WHERE Pr2.[Repository] = Iss.[Repository] AND (Pr2.[Status] = N'Open' OR Pr2.[Status] = N'Draft')
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
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[PullRequests_RemoveForRepositories]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @RepositoriesToRemove TABLE ([Repository] NVARCHAR(450) NOT NULL PRIMARY KEY);
  IF @repositories IS NULL
    BEGIN
      RETURN;
    END;
  INSERT INTO @RepositoriesToRemove ([Repository])
  SELECT DISTINCT [Source].[Repository]
  FROM (
    SELECT TRIM([value]) AS [Repository]
    FROM STRING_SPLIT(@repositories, N',')
  ) AS [Source]
  WHERE [Source].[Repository] > N'';
  DELETE FROM [dbo].[PullRequestAssignees]
  WHERE EXISTS (
      SELECT 1
      FROM @RepositoriesToRemove AS [Source]
      WHERE [Source].[Repository] = [dbo].[PullRequestAssignees].[Repository]
    );
  DELETE FROM [dbo].[PullRequests]
  WHERE EXISTS (
      SELECT 1
      FROM @RepositoriesToRemove AS [Source]
      WHERE [Source].[Repository] = [dbo].[PullRequests].[Repository]
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_RemoveForRepositories]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @RepositoriesToRemove TABLE ([Repository] NVARCHAR(450) NOT NULL PRIMARY KEY);
  IF @repositories IS NULL
    BEGIN
      RETURN;
    END;
  INSERT INTO @RepositoriesToRemove ([Repository])
  SELECT DISTINCT [Source].[Repository]
  FROM (
    SELECT TRIM([value]) AS [Repository]
    FROM STRING_SPLIT(@repositories, N',')
  ) AS [Source]
  WHERE [Source].[Repository] > N'';
  DELETE FROM [dbo].[IssueAssignees]
  WHERE EXISTS (
      SELECT 1
      FROM @RepositoriesToRemove AS [Source]
      WHERE [Source].[Repository] = [dbo].[IssueAssignees].[Repository]
    );
  DELETE FROM [dbo].[Issues]
  WHERE EXISTS (
      SELECT 1
      FROM @RepositoriesToRemove AS [Source]
      WHERE [Source].[Repository] = [dbo].[Issues].[Repository]
    );
END;
GO
