CREATE PROCEDURE [dbo].[DependencyBranches_GetDueForRecheck]
  @maxCount INT,
  @checkedBefore DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  SELECT TOP (@maxCount)
    Branches.[Repository],
    Branches.[Branch],
    Branches.[FirstSeen],
    Branches.[LastChecked],
    Branches.[LastUpdated],
    Branches.[PullRequestNumber],
    Branches.[DeletedAt],
    Branches.[ETag]
  FROM [dbo].[DependencyBranches] AS Branches
  WHERE Branches.[PullRequestNumber] IS NULL
    AND Branches.[DeletedAt] IS NULL
    AND (
      Branches.[LastChecked] < @checkedBefore
      OR (Branches.[LastChecked] IS NULL AND Branches.[FirstSeen] < @checkedBefore)
    )
  ORDER BY ISNULL(Branches.[LastChecked], Branches.[FirstSeen]), Branches.[Repository], Branches.[Branch];
END;
