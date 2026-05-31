CREATE PROCEDURE [dbo].[Issues_LinkPullRequest]
  @repository NVARCHAR(450),
  @id INT,
  @linkedPrNumber INT
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @now DATETIMEOFFSET = GETUTCDATE();
  UPDATE [dbo].[Issues]
  SET
    [LinkedPrNumber] = @linkedPrNumber,
    [LastUpdated] = @now
  WHERE [Repository] = @repository
    AND [Id] = @id
    AND [Status] = N'Open';
END;
