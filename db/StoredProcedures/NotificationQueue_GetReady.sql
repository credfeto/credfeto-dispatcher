CREATE PROCEDURE [dbo].[NotificationQueue_GetReady]
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    [SubjectUrl],
    [NotificationId],
    [Repository],
    [RepositoryUrl],
    [SubjectType],
    [SubjectTitle],
    [Reason],
    [UpdatedAt],
    [QueuedAt],
    [DispatchAfter]
  FROM [dbo].[NotificationQueue]
  WHERE [DispatchAfter] <= @now
  ORDER BY [QueuedAt];
END;
