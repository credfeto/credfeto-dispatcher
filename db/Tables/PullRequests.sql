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
  [HeadBranchName] NVARCHAR(MAX) NULL,
  CONSTRAINT [PK_PullRequests] PRIMARY KEY ([Repository], [Id])
);
