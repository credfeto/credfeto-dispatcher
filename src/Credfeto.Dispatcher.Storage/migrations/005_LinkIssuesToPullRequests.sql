CREATE OR ALTER PROCEDURE [dbo].[Issues_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(MAX),
  @priority INT,
  @isOnHold BIT,
  @linkedPrNumber INT,
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
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
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [LinkedPrNumber],
      [FirstSeen], [LastUpdated], [WhenClosed]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @linkedPrNumber,
      @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END
    );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Issues_LinkPullRequest]
  @repository NVARCHAR(450),
  @id INT,
  @linkedPrNumber INT,
  @now DATETIMEOFFSET
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE [dbo].[Issues]
  SET
    [LinkedPrNumber] = @linkedPrNumber,
    [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Id] = @id
    AND [Status] = N'Open';
END;
GO
