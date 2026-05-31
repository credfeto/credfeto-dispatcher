CREATE PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(MAX),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[Issues] AS [Target]
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
END;
