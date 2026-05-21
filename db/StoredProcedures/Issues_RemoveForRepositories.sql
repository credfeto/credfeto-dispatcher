CREATE PROCEDURE [dbo].[Issues_RemoveForRepositories]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  IF @repositories IS NULL
    BEGIN
      RETURN;
    END;
  WITH
    [RepositoryList] AS (
      SELECT TRIM([value]) AS [Repository]
      FROM STRING_SPLIT(@repositories, N',')
    )

  DELETE FROM [dbo].[Issues]
  WHERE [Repository] IN (
      SELECT [Repository] FROM [RepositoryList]
      WHERE [Repository] <> N''
    );
END;
