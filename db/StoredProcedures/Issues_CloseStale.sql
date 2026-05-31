CREATE PROCEDURE [dbo].[Issues_CloseStale]
  @repository NVARCHAR(450),
  @activeIssueIds NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  WITH
    [ActiveIds] AS (
      SELECT TRIM([value]) AS [CleanValue]
      FROM STRING_SPLIT(@activeIssueIds, N',')
    )

  UPDATE [dbo].[Issues]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now, [DateStatusChanged] = @now
  WHERE [Repository] = @repository
    AND [Status] = N'Open'
    AND (
      @activeIssueIds IS NULL
      OR NOT EXISTS (
        SELECT 1
        FROM [ActiveIds]
        WHERE TRY_CAST([ActiveIds].[CleanValue] AS INT) = [dbo].[Issues].[Id]
          AND [ActiveIds].[CleanValue] > N''
      )
    );
END;
