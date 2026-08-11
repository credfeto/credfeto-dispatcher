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
  WHERE ([Status] = N'Open' OR [Status] = N'Draft')
    AND [IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS Repo
      WHERE Repo.[Repository] = [PullRequests].[Repository] AND Repo.[IsActive] = 0
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_GetActive]
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
