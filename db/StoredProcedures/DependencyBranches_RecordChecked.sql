CREATE PROCEDURE [dbo].[DependencyBranches_RecordChecked]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[DependencyBranches]
  SET
    [LastChecked] = GETUTCDATE(),
    [ETag] = ISNULL(@eTag, [ETag])
  WHERE [Repository] = @repository AND [Branch] = @branch;
END;
