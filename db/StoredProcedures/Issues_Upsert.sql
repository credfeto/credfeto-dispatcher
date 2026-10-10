CREATE PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(16),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT,
  @assignees NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  BEGIN TRANSACTION;
  MERGE [dbo].[Issues] WITH (HOLDLOCK) AS [Target]
  USING (
    SELECT
      @repository AS [Repository],
      @id         AS [Id]
  ) AS [Source]
  ON [Target].[Repository] = [Source].[Repository] AND [Target].[Id] = [Source].[Id]
  WHEN MATCHED
    THEN
    UPDATE
      SET
        [Status] = @status,
        [Priority] = @priority,
        [IsOnHold] = @isOnHold,
        [LinkedPrNumber] = ISNULL(@linkedPrNumber, [Target].[LinkedPrNumber]),
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END,
        [DateStatusChanged] = CASE WHEN [Target].[Status] <> @status THEN @now ELSE [Target].[DateStatusChanged] END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [LinkedPrNumber],
      [FirstSeen], [LastUpdated], [WhenClosed], [DateStatusChanged]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @linkedPrNumber,
      @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END,
      @now
    );
  IF @assignees IS NOT NULL
    BEGIN
      DELETE FROM [dbo].[IssueAssignees] WITH (HOLDLOCK)
      WHERE [Repository] = @repository AND [Id] = @id;
      INSERT INTO [dbo].[IssueAssignees] ([Repository], [Id], [Login])
      SELECT DISTINCT
        @repository AS [Repository],
        @id         AS [Id],
        [Source].[Login]
      FROM (
        SELECT TRIM([value]) AS [Login]
        FROM STRING_SPLIT(@assignees, N',')
      ) AS [Source]
      WHERE [Source].[Login] > N'';
    END;
  COMMIT TRANSACTION;
END;
