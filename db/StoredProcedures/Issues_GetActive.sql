CREATE PROCEDURE [dbo].[Issues_GetActive]
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    Iss.[Repository],
    Iss.[Id],
    Iss.[Status],
    Iss.[FirstSeen],
    Iss.[LastUpdated],
    Iss.[WhenClosed],
    Iss.[Priority],
    Iss.[IsOnHold],
    Iss.[LinkedPrNumber]
  FROM [dbo].[Issues] AS Iss
  WHERE Iss.[Status] = N'Open'
    AND Iss.[IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS Repo
      WHERE Repo.[Repository] = Iss.[Repository] AND Repo.[IsActive] = 0
    )
    AND (
      Iss.[LinkedPrNumber] IS NULL
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequests] AS Pr
        WHERE Pr.[Repository] = Iss.[Repository]
          AND Pr.[Id] = Iss.[LinkedPrNumber]
          AND Pr.[Status] IN (N'Open', N'Draft')
      )
    )
    AND (
      Iss.[Priority] >= 4
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequests] AS Pr2
        WHERE Pr2.[Repository] = Iss.[Repository] AND Pr2.[Status] IN (N'Open', N'Draft')
      )
    );
END;
