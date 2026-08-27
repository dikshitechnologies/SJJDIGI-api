-- ============================================================
-- Migration 003
-- AppVersion table — stores the latest app version per platform.
-- Admin updates this row when a new build is released.
-- Run this ONCE against the production database.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID('dbo.AppVersion')
      AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.AppVersion (
        Id            INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        -- 'android' or 'ios'
        Platform      VARCHAR(20)   NOT NULL,
        -- Human-readable version string e.g. "1.1.0"
        Version       VARCHAR(20)   NOT NULL,
        -- Integer build number — THIS is what the app compares.
        -- Never compare version strings; always compare VersionCode.
        -- e.g. 10, 11, 12 ...
        VersionCode   INT           NOT NULL,
        -- 1 = user cannot skip, no "Later" button shown
        -- 0 = user can dismiss and continue with old version
        IsMandatory   BIT           NOT NULL DEFAULT 0,
        -- Message shown in the update popup
        UpdateMessage VARCHAR(500)  NULL,
        -- Direct store URL
        -- Android: https://play.google.com/store/apps/details?id=YOUR_PACKAGE
        -- iOS:     https://apps.apple.com/app/idXXXXXXXXXX
        StoreUrl      VARCHAR(1000) NULL,
        -- Set to 0 to disable version check without deleting the row
        IsActive      BIT           NOT NULL DEFAULT 1,
        CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt     DATETIME2     NULL
    );

    CREATE UNIQUE INDEX UQ_AppVersion_Platform
        ON dbo.AppVersion (Platform)
        WHERE IsActive = 1;
END
GO

-- ── Seed initial rows ────────────────────────────────────────
-- Update VersionCode and Version whenever you publish a new build.

IF NOT EXISTS (SELECT 1 FROM dbo.AppVersion WHERE Platform = 'android')
BEGIN
    INSERT INTO dbo.AppVersion
        (Platform, Version, VersionCode, IsMandatory, UpdateMessage,
         StoreUrl, IsActive)
    VALUES
        ('android', '1.0.0', 10, 0,
         'A new version is available. Please update to enjoy the latest features.',
         'https://play.google.com/store/apps/details?id=com.dikshitech.saroj',
         1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.AppVersion WHERE Platform = 'ios')
BEGIN
    INSERT INTO dbo.AppVersion
        (Platform, Version, VersionCode, IsMandatory, UpdateMessage,
         StoreUrl, IsActive)
    VALUES
        ('ios', '1.0.0', 10, 0,
         'A new version is available. Please update to enjoy the latest features.',
         'https://apps.apple.com/app/idXXXXXXXXXX',
         1);
END
GO

-- ── How to release a new version ────────────────────────────
-- When you publish build 11 (version 1.1.0) on Play Store:
--
--   UPDATE dbo.AppVersion
--   SET Version       = '1.1.0',
--       VersionCode   = 11,
--       IsMandatory   = 0,
--       UpdateMessage = 'New features and bug fixes.',
--       UpdatedAt     = SYSUTCDATETIME()
--   WHERE Platform = 'android';
--
-- For a mandatory security update:
--
--   UPDATE dbo.AppVersion
--   SET Version       = '1.2.0',
--       VersionCode   = 12,
--       IsMandatory   = 1,
--       UpdateMessage = 'Critical update required. Please update to continue.',
--       UpdatedAt     = SYSUTCDATETIME()
--   WHERE Platform = 'android';
