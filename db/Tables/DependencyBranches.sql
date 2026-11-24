CREATE TABLE [dbo].[DependencyBranches] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Branch] NVARCHAR(400) NOT NULL,
  [FirstSeen] DATETIMEOFFSET NOT NULL,
  [LastChecked] DATETIMEOFFSET NULL,
  [LastUpdated] DATETIMEOFFSET NOT NULL,
  [PullRequestNumber] INT NULL,
  [DeletedAt] DATETIMEOFFSET NULL,
  [ETag] NVARCHAR(1024) NULL,
  CONSTRAINT [PK_DependencyBranches] PRIMARY KEY NONCLUSTERED ([Repository], [Branch]),
  INDEX [IX_DependencyBranches_Repository] CLUSTERED ([Repository])
);
