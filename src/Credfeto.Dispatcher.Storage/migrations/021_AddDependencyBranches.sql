IF OBJECT_ID(N'[dbo].[DependencyBranches]', N'U') IS NULL
  BEGIN
    CREATE TABLE [dbo].[DependencyBranches] (
      [Repository] NVARCHAR(450) NOT NULL,
      [Branch] NVARCHAR(400) NOT NULL,
      [FirstSeen] DATETIMEOFFSET NOT NULL,
      [LastChecked] DATETIMEOFFSET NULL,
      [LastUpdated] DATETIMEOFFSET NOT NULL,
      [PullRequestNumber] INT NULL,
      [DeletedAt] DATETIMEOFFSET NULL,
      [ETag] NVARCHAR(1024) NULL,
      CONSTRAINT [PK_DependencyBranches] PRIMARY KEY NONCLUSTERED ([Repository], [Branch]),
      INDEX [IX_DependencyBranches_Repository] CLUSTERED ([Repository])
    );
  END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_RecordCreated]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @createdAt DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  -- A branch deleted before this creation was re-created, so it is tracked afresh; otherwise an existing row stays.
  MERGE [dbo].[DependencyBranches] WITH (HOLDLOCK) AS [Target]
  USING (
    SELECT
      @repository AS [Repository],
      @branch     AS [Branch]
  ) AS [Source]
  ON [Target].[Repository] = [Source].[Repository] AND [Target].[Branch] = [Source].[Branch]
  WHEN MATCHED AND [Target].[DeletedAt] IS NOT NULL AND [Target].[DeletedAt] < @createdAt
    THEN
    UPDATE
      SET
        [FirstSeen] = @createdAt,
        [LastChecked] = NULL,
        [LastUpdated] = @now,
        [PullRequestNumber] = NULL,
        [DeletedAt] = NULL,
        [ETag] = NULL
  WHEN NOT MATCHED
    THEN
    INSERT ([Repository], [Branch], [FirstSeen], [LastChecked], [LastUpdated], [PullRequestNumber], [DeletedAt], [ETag])
    VALUES (@repository, @branch, @createdAt, NULL, @now, NULL, NULL, NULL);
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_RecordDeleted]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @deletedAt DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[DependencyBranches]
  SET
    [DeletedAt] = @deletedAt,
    [LastUpdated] = GETUTCDATE()
  WHERE [Repository] = @repository
    AND [Branch] = @branch
    AND [DeletedAt] IS NULL
    AND [FirstSeen] <= @deletedAt;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_RecordPullRequest]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @pullRequestNumber INT
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[DependencyBranches]
  SET
    [PullRequestNumber] = @pullRequestNumber,
    [LastUpdated] = GETUTCDATE()
  WHERE [Repository] = @repository AND [Branch] = @branch;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_GetDueForRecheck]
  @maxCount INT,
  @checkedBefore DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  SELECT TOP (@maxCount)
    Branches.[Repository],
    Branches.[Branch],
    Branches.[FirstSeen],
    Branches.[LastChecked],
    Branches.[LastUpdated],
    Branches.[PullRequestNumber],
    Branches.[DeletedAt],
    Branches.[ETag]
  FROM [dbo].[DependencyBranches] AS Branches
  WHERE Branches.[PullRequestNumber] IS NULL
    AND Branches.[DeletedAt] IS NULL
    AND (
      Branches.[LastChecked] < @checkedBefore
      OR (Branches.[LastChecked] IS NULL AND Branches.[FirstSeen] < @checkedBefore)
    )
  ORDER BY ISNULL(Branches.[LastChecked], Branches.[FirstSeen]), Branches.[Repository], Branches.[Branch];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_RecordChecked]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[DependencyBranches]
  SET
    [LastChecked] = GETUTCDATE(),
    [ETag] = ISNULL(@eTag, [ETag])
  WHERE [Repository] = @repository AND [Branch] = @branch;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_Prune]
  @settledBefore DATETIMEOFFSET,
  @firstSeenBefore DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DELETE FROM [dbo].[DependencyBranches]
  WHERE (
    ([PullRequestNumber] IS NOT NULL OR [DeletedAt] IS NOT NULL)
    AND [LastUpdated] < @settledBefore
  )
  OR [FirstSeen] < @firstSeenBefore;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[DependencyBranches_GetOrphaned]
  @owner NVARCHAR(100),
  @repo NVARCHAR(100),
  @firstSeenBefore DATETIMEOFFSET
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
    Branches.[Repository],
    Branches.[Branch],
    Branches.[FirstSeen],
    Branches.[LastChecked],
    Branches.[LastUpdated],
    Branches.[PullRequestNumber],
    Branches.[DeletedAt],
    Branches.[ETag]
  FROM [dbo].[DependencyBranches] AS Branches
  WHERE Branches.[Repository] LIKE @repositoryPattern ESCAPE N'\'
    AND Branches.[PullRequestNumber] IS NULL
    AND Branches.[DeletedAt] IS NULL
    AND Branches.[FirstSeen] < @firstSeenBefore
  ORDER BY Branches.[FirstSeen], Branches.[Repository], Branches.[Branch];
END;
GO
