CREATE PROCEDURE [dbo].[NotificationQueue_Delete]
  @subjectUrl NVARCHAR(450)
AS
BEGIN
  SET NOCOUNT ON;
  DELETE FROM [dbo].[NotificationQueue]
  WHERE [SubjectUrl] = @subjectUrl;
END;
