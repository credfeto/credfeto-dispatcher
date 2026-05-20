CREATE TABLE [dbo].[PullRequests] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Id] INT NOT NULL,
  [Status] NVARCHAR(MAX) NOT NULL,
  [FirstSeen] DATETIMEOFFSET NOT NULL,
  [LastUpdated] DATETIMEOFFSET NOT NULL,
  [WhenClosed] DATETIMEOFFSET NULL,
  [Priority] INT NOT NULL DEFAULT 0,
  [IsOnHold] BIT NOT NULL DEFAULT 0,
  [CommentCount] INT NOT NULL DEFAULT 0,
  [ReviewDecision] NVARCHAR(MAX) NULL,
  [FailedCheckCount] INT NOT NULL DEFAULT 0,
  [FailedCheckNames] NVARCHAR(MAX) NULL,
  [FailedCheckSha] NVARCHAR(MAX) NULL,
  [Author] NVARCHAR(MAX) NULL,
  CONSTRAINT [PK_PullRequests] PRIMARY KEY ([Repository], [Id])
);

CREATE TABLE [dbo].[Issues] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Id] INT NOT NULL,
  [Status] NVARCHAR(MAX) NOT NULL,
  [FirstSeen] DATETIMEOFFSET NOT NULL,
  [LastUpdated] DATETIMEOFFSET NOT NULL,
  [WhenClosed] DATETIMEOFFSET NULL,
  [Priority] INT NOT NULL DEFAULT 0,
  [IsOnHold] BIT NOT NULL DEFAULT 0,
  [LinkedPrNumber] INT NULL,
  CONSTRAINT [PK_Issues] PRIMARY KEY ([Repository], [Id])
);

CREATE TABLE [dbo].[Repos] (
  [Repository] NVARCHAR(450) NOT NULL,
  [IsActive] BIT NOT NULL DEFAULT 1,
  [LastUpdated] DATETIMEOFFSET NOT NULL,
  CONSTRAINT [PK_Repos] PRIMARY KEY ([Repository])
);

CREATE TABLE [dbo].[PollingStates] (
  [Key] NVARCHAR(256) NOT NULL,
  [ETag] NVARCHAR(1024) NOT NULL,
  CONSTRAINT [PK_PollingStates] PRIMARY KEY ([Key])
);

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
