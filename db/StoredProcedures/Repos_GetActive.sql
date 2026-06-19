CREATE PROCEDURE [dbo].[Repos_GetActive]
AS
BEGIN
  SET NOCOUNT ON;
  SELECT [Repository]
  FROM [dbo].[Repos]
  WHERE [IsActive] = 1;
END;
