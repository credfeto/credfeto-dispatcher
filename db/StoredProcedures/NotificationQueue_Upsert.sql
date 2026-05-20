CREATE PROCEDURE [dbo].[NotificationQueue_Upsert]
  @subjectUrl NVARCHAR(450),
  @notificationId NVARCHAR(MAX),
  @repository NVARCHAR(450),
  @repositoryUrl NVARCHAR(450),
  @subjectType NVARCHAR(MAX),
  @subjectTitle NVARCHAR(MAX),
  @reason NVARCHAR(MAX),
  @updatedAt DATETIMEOFFSET,
  @queuedAt DATETIMEOFFSET,
  @dispatchAfter DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  MERGE [dbo].[NotificationQueue] AS [Target]
  USING (SELECT @subjectUrl AS [SubjectUrl]) AS [Source] ON [Target].[SubjectUrl] = [Source].[SubjectUrl]
  WHEN MATCHED
    THEN
    UPDATE
      SET
        [NotificationId] = @notificationId,
        [Reason] = @reason,
        [SubjectTitle] = @subjectTitle,
        [UpdatedAt] = @updatedAt,
        [QueuedAt] = @queuedAt,
        [DispatchAfter] = @dispatchAfter
  WHEN NOT MATCHED
    THEN
    INSERT (
      [SubjectUrl], [NotificationId], [Repository], [RepositoryUrl], [SubjectType],
      [SubjectTitle], [Reason], [UpdatedAt], [QueuedAt], [DispatchAfter]
    )
    VALUES (
      @subjectUrl, @notificationId, @repository, @repositoryUrl, @subjectType,
      @subjectTitle, @reason, @updatedAt, @queuedAt, @dispatchAfter
    );
END;
