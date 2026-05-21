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
    AND [Status] <> N'Closed'
    AND (@activePrIds IS NULL OR [Id] NOT IN (
      SELECT TRY_CAST([CleanValue] AS INT)
      FROM [ActiveIds]
      WHERE [CleanValue] <> N''
    ));
END;
