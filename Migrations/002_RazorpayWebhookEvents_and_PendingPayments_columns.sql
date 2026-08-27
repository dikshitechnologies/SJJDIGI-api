    -- ============================================================
    -- Migration 002
    -- 1. Add missing columns to PendingPayments (if not exists)
    -- 2. Create RazorpayWebhookEvents idempotency table
    -- Run this ONCE against the production database.
    -- ============================================================

    -- ── 1. PendingPayments column additions ─────────────────────
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.PendingPayments')
        AND name = 'RazorpayPaymentId'
    )
        ALTER TABLE dbo.PendingPayments
            ADD RazorpayPaymentId NVARCHAR(100) NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.PendingPayments')
        AND name = 'ProcessedAt'
    )
        ALTER TABLE dbo.PendingPayments
            ADD ProcessedAt DATETIME2 NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.PendingPayments')
        AND name = 'ErrorMessage'
    )
        ALTER TABLE dbo.PendingPayments
            ADD ErrorMessage NVARCHAR(2000) NULL;

    -- Ensure Status column is wide enough for 'needs_review'
    -- (No-op if already correct size)

    -- ── 2. RazorpayWebhookEvents — one row per X-Razorpay-Event-Id ─
    IF NOT EXISTS (
        SELECT 1 FROM sys.objects
        WHERE object_id = OBJECT_ID('dbo.RazorpayWebhookEvents')
        AND type = 'U'
    )
    BEGIN
        CREATE TABLE dbo.RazorpayWebhookEvents (
            Id            INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
            -- Razorpay's unique event identifier (X-Razorpay-Event-Id header).
            -- UNIQUE constraint is the idempotency key — duplicate webhooks are
            -- silently dropped at the DB level.
            EventId       NVARCHAR(100)  NOT NULL,
            EventType     NVARCHAR(100)  NOT NULL,   -- e.g. 'payment.captured' 
            PaymentId     NVARCHAR(100)  NULL,
            OrderId       NVARCHAR(100)  NULL,
            ReceivedAt    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
            ProcessedAt   DATETIME2      NULL,
            -- 'received' | 'processed' | 'duplicate' | 'failed' | 'needs_review'
            Status        NVARCHAR(50)   NOT NULL DEFAULT 'received',
            ErrorMessage  NVARCHAR(2000) NULL,
            CONSTRAINT UQ_RazorpayWebhookEvents_EventId UNIQUE (EventId)
        );

        CREATE INDEX IX_RazorpayWebhookEvents_PaymentId
            ON dbo.RazorpayWebhookEvents (PaymentId);

        CREATE INDEX IX_RazorpayWebhookEvents_OrderId
            ON dbo.RazorpayWebhookEvents (OrderId);
    END
    GO
