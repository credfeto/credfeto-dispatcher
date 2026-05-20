CREATE PROCEDURE [dbo].[Issues_CloseStale]
  @repository NVARCHAR(450),
  @activeIssueIds NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[Issues]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Status] <> N'Closed'
    AND (@activeIssueIds IS NULL OR [Id] NOT IN (
      SELECT TRY_CAST(TRIM([value]) AS INT)
      FROM STRING_SPLIT(@activeIssueIds, N',')
      WHERE LEN(TRIM([value])) > 0
    ));
END;
