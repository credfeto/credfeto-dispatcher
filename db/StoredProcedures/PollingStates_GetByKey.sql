CREATE PROCEDURE [dbo].[PollingStates_GetByKey]
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
