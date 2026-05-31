CREATE PROCEDURE [dbo].[PollingStates_Upsert]
  @key NVARCHAR(256),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  MERGE [dbo].[PollingStates] AS [Target]
  USING (SELECT @key AS [Key]) AS [Source] ON [Target].[Key] = [Source].[Key]
  WHEN MATCHED THEN UPDATE SET
    [ETag] = @eTag,
    [DateUpdated] = @now,
    [DateStateChanged] = CASE WHEN [Target].[ETag] <> @eTag THEN @now ELSE [Target].[DateStateChanged] END
  WHEN NOT MATCHED THEN INSERT ([Key], [ETag], [DateCreated], [DateUpdated], [DateStateChanged])
    VALUES (@key, @eTag, @now, @now, @now);
END;
