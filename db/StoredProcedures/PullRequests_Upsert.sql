CREATE PROCEDURE [dbo].[PullRequests_Upsert]
  @repository NVARCHAR(450),
  @id INT,
  @status NVARCHAR(16),
  @priority INT,
  @isOnHold BIT,
  @hasDetail BIT,
  @commentCount INT,
  @reviewDecision NVARCHAR(MAX),
  @failedCheckCount INT,
  @failedCheckNames NVARCHAR(MAX),
  @failedCheckSha NVARCHAR(MAX),
  @author NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[PullRequests] WITH (HOLDLOCK) AS [Target]
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
        [CommentCount] = CASE WHEN @hasDetail = 1 THEN @commentCount ELSE [Target].[CommentCount] END,
        [ReviewDecision] = CASE WHEN @hasDetail = 1 THEN @reviewDecision ELSE [Target].[ReviewDecision] END,
        [FailedCheckCount] = CASE WHEN @hasDetail = 1 THEN @failedCheckCount ELSE [Target].[FailedCheckCount] END,
        [FailedCheckNames] = CASE WHEN @hasDetail = 1 THEN @failedCheckNames ELSE [Target].[FailedCheckNames] END,
        [FailedCheckSha] = CASE WHEN @hasDetail = 1 THEN @failedCheckSha ELSE [Target].[FailedCheckSha] END,
        [Author] = ISNULL(@author, [Target].[Author]),
        [LastUpdated] = @now,
        [WhenClosed] = CASE WHEN @status = N'Closed' THEN ISNULL([Target].[WhenClosed], @now) END,
        [DateStatusChanged] = CASE WHEN [Target].[Status] <> @status THEN @now ELSE [Target].[DateStatusChanged] END
  WHEN NOT MATCHED
    THEN
    INSERT (
      [Repository], [Id], [Status], [Priority], [IsOnHold], [CommentCount],
      [ReviewDecision], [FailedCheckCount], [FailedCheckNames], [FailedCheckSha],
      [Author], [FirstSeen], [LastUpdated], [WhenClosed], [DateStatusChanged]
    )
    VALUES (
      @repository, @id, @status, @priority, @isOnHold, @commentCount,
      @reviewDecision, @failedCheckCount, @failedCheckNames, @failedCheckSha,
      @author, @now, @now,
      CASE WHEN @status = N'Closed' THEN @now END,
      @now
    );
END;
