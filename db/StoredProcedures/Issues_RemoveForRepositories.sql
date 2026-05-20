CREATE PROCEDURE [dbo].[Issues_RemoveForRepositories]
  @repositories NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  IF @repositories IS NULL
    BEGIN
      RETURN;
    END;
  DELETE FROM [dbo].[Issues]
  WHERE [Repository] IN (
      SELECT TRIM([value]) FROM STRING_SPLIT(@repositories, N',')
      WHERE LEN(TRIM([value])) > 0
    );
END;
