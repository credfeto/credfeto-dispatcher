CREATE PROCEDURE [dbo].[PullRequests_RemoveForRepositories]
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
