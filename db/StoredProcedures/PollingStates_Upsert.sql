CREATE PROCEDURE [dbo].[PollingStates_Upsert]
  @key NVARCHAR(256),
  @eTag NVARCHAR(1024)
AS
BEGIN
  SET NOCOUNT ON;
  MERGE [dbo].[PollingStates] AS [Target]
  USING (SELECT @key AS [Key]) AS [Source] ON [Target].[Key] = [Source].[Key]
  WHEN MATCHED THEN UPDATE SET [ETag] = @eTag
  WHEN NOT MATCHED THEN INSERT ([Key], [ETag]) VALUES (@key, @eTag);
END;
