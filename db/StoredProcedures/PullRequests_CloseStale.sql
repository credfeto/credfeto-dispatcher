CREATE PROCEDURE [dbo].[PullRequests_CloseStale]
  @repository NVARCHAR(450),
  @activePrIds NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  WITH
    [ActiveIds] AS (
      SELECT TRIM([value]) AS [CleanValue]
      FROM STRING_SPLIT(@activePrIds, N',')
    )

  UPDATE [dbo].[PullRequests]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Status] IN (N'Open', N'Draft')
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
