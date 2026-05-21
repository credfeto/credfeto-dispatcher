CREATE PROCEDURE [dbo].[Issues_GetActive]
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    I.[Repository],
    I.[Id],
    I.[Status],
    I.[FirstSeen],
    I.[LastUpdated],
    I.[WhenClosed],
    I.[Priority],
    I.[IsOnHold],
    I.[LinkedPrNumber]
  FROM [dbo].[Issues] AS I
  WHERE I.[Status] <> N'Closed'
    AND I.[IsOnHold] = 0
    AND NOT EXISTS (
      SELECT 1 FROM [dbo].[Repos] AS R
      WHERE R.[Repository] = I.[Repository] AND R.[IsActive] = 0
    )
    AND (
      I.[LinkedPrNumber] IS NULL
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequests] AS Pr
        WHERE Pr.[Repository] = I.[Repository]
          AND Pr.[Id] = I.[LinkedPrNumber]
          AND Pr.[Status] <> N'Closed'
      )
    )
    AND (
      I.[Priority] >= 4
      OR NOT EXISTS (
        SELECT 1 FROM [dbo].[PullRequests] AS Pr2
        WHERE Pr2.[Repository] = I.[Repository] AND Pr2.[Status] <> N'Closed'
      )
    );
END;
