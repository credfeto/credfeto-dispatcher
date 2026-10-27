CREATE TABLE [dbo].[PullRequestCommitAuthors] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Id] INT NOT NULL,
  [Login] NVARCHAR(100) NOT NULL,
  CONSTRAINT [PK_PullRequestCommitAuthors] PRIMARY KEY NONCLUSTERED ([Repository], [Id], [Login]),
  INDEX [IX_PullRequestCommitAuthors_WorkItem] CLUSTERED ([Repository], [Id])
);
