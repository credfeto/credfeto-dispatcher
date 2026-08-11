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
  SELECT [Source].[Repository]
  FROM (
    SELECT TRIM([value]) AS [Repository]
    FROM STRING_SPLIT(@repositories, N',')
  ) AS [Source]
  WHERE [Source].[Repository] > N'';
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
  SELECT [Source].[Repository]
  FROM (
    SELECT TRIM([value]) AS [Repository]
    FROM STRING_SPLIT(@repositories, N',')
  ) AS [Source]
  WHERE [Source].[Repository] > N'';
  DELETE FROM [dbo].[Issues]
  WHERE EXISTS (
      SELECT 1
      FROM @RepositoriesToRemove AS [Source]
      WHERE [Source].[Repository] = [dbo].[Issues].[Repository]
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Repos_SetActive]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  DECLARE @ActiveRepos TABLE ([Repository] NVARCHAR(450) NOT NULL);
  IF @repositories IS NOT NULL
    BEGIN
      INSERT INTO @ActiveRepos ([Repository])
      SELECT [Source].[Repository]
      FROM (
        SELECT TRIM([value]) AS [Repository]
        FROM STRING_SPLIT(@repositories, N',')
      ) AS [Source]
      WHERE [Source].[Repository] > N'';
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
    UPDATE SET [IsActive] = 1, [LastUpdated] = @now
  WHEN NOT MATCHED BY TARGET
    THEN
    INSERT ([Repository], [IsActive], [LastUpdated])
    VALUES ([Source].[Repository], 1, @now)
  WHEN NOT MATCHED BY SOURCE
    THEN
    UPDATE SET [IsActive] = 0, [LastUpdated] = @now;
END;
GO
