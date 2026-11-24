CREATE PROCEDURE [dbo].[DependencyBranches_RecordCreated]
  @repository NVARCHAR(450),
  @branch NVARCHAR(400),
  @createdAt DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  -- A branch deleted before this creation was re-created, so it is tracked afresh; otherwise an existing row stays.
  MERGE [dbo].[DependencyBranches] WITH (HOLDLOCK) AS [Target]
  USING (
    SELECT
      @repository AS [Repository],
      @branch     AS [Branch]
  ) AS [Source]
  ON [Target].[Repository] = [Source].[Repository] AND [Target].[Branch] = [Source].[Branch]
  WHEN MATCHED AND [Target].[DeletedAt] IS NOT NULL AND [Target].[DeletedAt] < @createdAt
    THEN
    UPDATE
      SET
        [FirstSeen] = @createdAt,
        [LastChecked] = NULL,
        [LastUpdated] = @now,
        [PullRequestNumber] = NULL,
        [DeletedAt] = NULL,
        [ETag] = NULL
  WHEN NOT MATCHED
    THEN
    INSERT ([Repository], [Branch], [FirstSeen], [LastChecked], [LastUpdated], [PullRequestNumber], [DeletedAt], [ETag])
    VALUES (@repository, @branch, @createdAt, NULL, @now, NULL, NULL, NULL);
END;
