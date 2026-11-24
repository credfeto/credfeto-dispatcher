CREATE PROCEDURE [dbo].[DependencyBranches_GetOrphaned]
  @owner NVARCHAR(100),
  @repo NVARCHAR(100),
  @firstSeenBefore DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  -- Repository is always 'owner/repo'; owner logins cannot contain LIKE wildcards, repo names can contain '_'.
  DECLARE
    @repositoryPattern NVARCHAR(450)
    = CASE
      WHEN @owner IS NULL AND @repo IS NULL THEN N'%'
      WHEN @repo IS NULL THEN @owner + N'/%'
      WHEN @owner IS NULL THEN N'%/' + REPLACE(@repo, N'_', N'\_')
      ELSE @owner + N'/' + REPLACE(@repo, N'_', N'\_')
    END;
  SELECT
    Branches.[Repository],
    Branches.[Branch],
    Branches.[FirstSeen],
    Branches.[LastChecked],
    Branches.[LastUpdated],
    Branches.[PullRequestNumber],
    Branches.[DeletedAt],
    Branches.[ETag]
  FROM [dbo].[DependencyBranches] AS Branches
  WHERE Branches.[Repository] LIKE @repositoryPattern ESCAPE N'\'
    AND Branches.[PullRequestNumber] IS NULL
    AND Branches.[DeletedAt] IS NULL
    AND Branches.[FirstSeen] < @firstSeenBefore
  ORDER BY Branches.[FirstSeen], Branches.[Repository], Branches.[Branch];
END;
