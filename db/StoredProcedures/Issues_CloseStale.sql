CREATE PROCEDURE [dbo].[Issues_CloseStale]
  @repository NVARCHAR(450),
  @activeIssueIds NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  WITH
    [ActiveIds] AS (
      SELECT TRIM([value]) AS [CleanValue]
      FROM STRING_SPLIT(@activeIssueIds, N',')
    )

  UPDATE [dbo].[Issues]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Status] <> N'Closed'
    AND (@activeIssueIds IS NULL OR [Id] NOT IN (
      SELECT TRY_CAST([CleanValue] AS INT)
      FROM [ActiveIds]
      WHERE [CleanValue] <> N''
    ));
END;
