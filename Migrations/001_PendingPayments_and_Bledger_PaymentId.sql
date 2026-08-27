-- =============================================================================
-- Migration: Razorpay webhook support
-- Run this once against your live database before deploying the updated API.
-- =============================================================================

-- 1. Add FRazorpayPaymentId column to Bledger for idempotency lookups.
--    Allows the webhook (and retry logic) to detect an already-inserted payment.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Bledger')
      AND name = 'FRazorpayPaymentId'
)
BEGIN
    ALTER TABLE dbo.Bledger
        ADD FRazorpayPaymentId NVARCHAR(100) NULL;

    -- Index makes the idempotency SELECT fast even with many rows.
    CREATE INDEX IX_Bledger_RazorpayPaymentId
        ON dbo.Bledger (FRazorpayPaymentId)
        WHERE FRazorpayPaymentId IS NOT NULL;
END
GO

-- 2. PendingPayments table
--    Stores the InsertChitScheme payload alongside the Razorpay orderId.
--    The webhook reads this row when `payment.captured` fires, so it has
--    all the context needed to do the insert even if the app was killed.
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('dbo.PendingPayments') AND type = 'U')
BEGIN
    CREATE TABLE dbo.PendingPayments (
        Id                  INT             IDENTITY(1,1) PRIMARY KEY,
        RazorpayOrderId     NVARCHAR(100)   NOT NULL,           -- rzp order id (key for webhook lookup)
        RazorpayPaymentId   NVARCHAR(100)   NULL,               -- filled in by webhook on capture
        UserId              NVARCHAR(100)   NULL,               -- RegisterUsers.UserID
        ChitPayload         NVARCHAR(MAX)   NOT NULL,           -- JSON of ChitSchemeModel (minus PaymentId)
        Status              NVARCHAR(20)    NOT NULL DEFAULT 'pending',  -- pending | completed | failed
        CreatedAt           DATETIME        NOT NULL DEFAULT GETDATE(),
        ProcessedAt         DATETIME        NULL,
        ErrorMessage        NVARCHAR(MAX)   NULL
    );

    CREATE UNIQUE INDEX UX_PendingPayments_OrderId
        ON dbo.PendingPayments (RazorpayOrderId);

    -- Fast lookup by payment id after webhook fires
    CREATE INDEX IX_PendingPayments_PaymentId
        ON dbo.PendingPayments (RazorpayPaymentId)
        WHERE RazorpayPaymentId IS NOT NULL;
END
GO
