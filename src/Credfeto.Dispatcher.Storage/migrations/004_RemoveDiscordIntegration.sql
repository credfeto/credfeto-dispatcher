IF OBJECT_ID(N'[dbo].[NotificationQueue_Upsert]', N'P') IS NOT NULL
  BEGIN
    DROP PROCEDURE [dbo].[NotificationQueue_Upsert];
  END;
GO

IF OBJECT_ID(N'[dbo].[NotificationQueue_Delete]', N'P') IS NOT NULL
  BEGIN
    DROP PROCEDURE [dbo].[NotificationQueue_Delete];
  END;
GO

IF OBJECT_ID(N'[dbo].[NotificationQueue_GetReady]', N'P') IS NOT NULL
  BEGIN
    DROP PROCEDURE [dbo].[NotificationQueue_GetReady];
  END;
GO

IF OBJECT_ID(N'[dbo].[NotificationQueue]', N'U') IS NOT NULL
  BEGIN
    DROP TABLE [dbo].[NotificationQueue];
  END;
GO
