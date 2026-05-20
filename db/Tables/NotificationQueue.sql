CREATE TABLE [dbo].[NotificationQueue] (
  [SubjectUrl] NVARCHAR(450) NOT NULL,
  [NotificationId] NVARCHAR(MAX) NOT NULL,
  [Repository] NVARCHAR(450) NOT NULL,
  [RepositoryUrl] NVARCHAR(450) NOT NULL,
  [SubjectType] NVARCHAR(MAX) NOT NULL,
  [SubjectTitle] NVARCHAR(MAX) NOT NULL,
  [Reason] NVARCHAR(MAX) NOT NULL,
  [UpdatedAt] DATETIMEOFFSET NOT NULL,
  [QueuedAt] DATETIMEOFFSET NOT NULL,
  [DispatchAfter] DATETIMEOFFSET NOT NULL,
  CONSTRAINT [PK_NotificationQueue] PRIMARY KEY ([SubjectUrl])
);
