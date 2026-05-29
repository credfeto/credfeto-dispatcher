CREATE PROCEDURE [dbo].[Issues_LinkPullRequest]
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
