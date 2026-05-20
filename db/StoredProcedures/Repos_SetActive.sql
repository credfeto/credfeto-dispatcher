CREATE PROCEDURE [dbo].[Repos_SetActive]
  @repositories NVARCHAR(MAX),
  @lastUpdated DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @ActiveRepos TABLE ([Repository] NVARCHAR(450) NOT NULL);
  IF @repositories IS NOT NULL
    BEGIN
      INSERT INTO @ActiveRepos ([Repository])
      SELECT TRIM([value]) FROM STRING_SPLIT(@repositories, N',')
      WHERE LEN(TRIM([value])) > 0;
    END;
  MERGE [dbo].[Repos] AS [Target]
  USING (SELECT
    [Repository],
    1 AS [IsActive]
  FROM @ActiveRepos) AS [Source]
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
