CREATE PROCEDURE [dbo].[DependencyBranches_Prune]
  @settledBefore DATETIMEOFFSET,
  @firstSeenBefore DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DELETE FROM [dbo].[DependencyBranches]
  WHERE (
    ([PullRequestNumber] IS NOT NULL OR [DeletedAt] IS NOT NULL)
    AND [LastUpdated] < @settledBefore
  )
  OR [FirstSeen] < @firstSeenBefore;
END;
