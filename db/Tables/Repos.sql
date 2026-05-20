CREATE TABLE [dbo].[Repos] (
  [Repository] NVARCHAR(450) NOT NULL,
  [IsActive] BIT NOT NULL DEFAULT 1,
  [LastUpdated] DATETIMEOFFSET NOT NULL,
  CONSTRAINT [PK_Repos] PRIMARY KEY ([Repository])
);
