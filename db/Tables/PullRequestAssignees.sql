CREATE TABLE [dbo].[PullRequestAssignees] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Id] INT NOT NULL,
  [Login] NVARCHAR(100) NOT NULL,
  CONSTRAINT [PK_PullRequestAssignees] PRIMARY KEY NONCLUSTERED ([Repository], [Id], [Login]),
  INDEX [IX_PullRequestAssignees_WorkItem] CLUSTERED ([Repository], [Id])
);
