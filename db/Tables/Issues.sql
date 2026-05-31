CREATE TABLE [dbo].[Issues] (
  [Repository] NVARCHAR(450) NOT NULL,
  [Id] INT NOT NULL,
  [Status] NVARCHAR(MAX) NOT NULL,
  [FirstSeen] DATETIMEOFFSET NOT NULL,
  [LastUpdated] DATETIMEOFFSET NOT NULL,
  [WhenClosed] DATETIMEOFFSET NULL,
  [Priority] INT NOT NULL DEFAULT 0,
  [IsOnHold] BIT NOT NULL DEFAULT 0,
  [LinkedPrNumber] INT NULL,
  [DateStatusChanged] DATETIMEOFFSET NULL,
  CONSTRAINT [PK_Issues] PRIMARY KEY ([Repository], [Id])
);
