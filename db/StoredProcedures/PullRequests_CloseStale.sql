CREATE PROCEDURE [dbo].[PullRequests_CloseStale]
  @repository NVARCHAR(450),
  @activePrIds NVARCHAR(MAX),
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[PullRequests]
  SET [Status] = N'Closed', [WhenClosed] = @now, [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Status] <> N'Closed'
    AND (@activePrIds IS NULL OR [Id] NOT IN (
      SELECT TRY_CAST(TRIM([value]) AS INT)
      FROM STRING_SPLIT(@activePrIds, N',')
      WHERE LEN(TRIM([value])) > 0
    ));
END;
