CREATE PROCEDURE [dbo].[PullRequests_CloseStale]
  @repository NVARCHAR(450),
  @activePrIds NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  WITH
    [ActiveIds] AS (
      SELECT TRIM([value]) AS [CleanValue]
      FROM STRING_SPLIT(@activePrIds, N',')
    )

  UPDATE [dbo].[PullRequests]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now, [DateStatusChanged] = @now
  WHERE [Repository] = @repository
    AND ([Status] = N'Open' OR [Status] = N'Draft')
    AND (
      @activePrIds IS NULL
      OR NOT EXISTS (
        SELECT 1
        FROM [ActiveIds]
        WHERE TRY_CAST([ActiveIds].[CleanValue] AS INT) = [dbo].[PullRequests].[Id]
          AND [ActiveIds].[CleanValue] > N''
      )
    );
END;
