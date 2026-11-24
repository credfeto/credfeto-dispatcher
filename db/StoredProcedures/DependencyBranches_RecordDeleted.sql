CREATE PROCEDURE [dbo].[DependencyBranches_RecordDeleted]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @deletedAt DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[DependencyBranches]
  SET
    [DeletedAt] = @deletedAt,
    [LastUpdated] = GETUTCDATE()
  WHERE [Repository] = @repository
    AND [Branch] = @branch
    AND [DeletedAt] IS NULL
    AND [FirstSeen] <= @deletedAt;
END;
