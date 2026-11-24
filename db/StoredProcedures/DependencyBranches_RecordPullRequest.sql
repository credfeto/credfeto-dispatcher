CREATE PROCEDURE [dbo].[DependencyBranches_RecordPullRequest]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @pullRequestNumber INT
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[DependencyBranches]
  SET
    [PullRequestNumber] = @pullRequestNumber,
    [LastUpdated] = GETUTCDATE()
  WHERE [Repository] = @repository AND [Branch] = @branch;
END;
