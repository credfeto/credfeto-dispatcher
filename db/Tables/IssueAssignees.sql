CREATE TABLE [dbo].[IssueAssignees] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Id] INT NOT NULL,
  [Login] NVARCHAR(100) NOT NULL,
  CONSTRAINT [PK_IssueAssignees] PRIMARY KEY NONCLUSTERED ([Repository], [Id], [Login]),
  INDEX [IX_IssueAssignees_WorkItem] CLUSTERED ([Repository], [Id])
);
