-- Migration 005: tenant_payments
-- QNB Finansbank 3DPay ödeme geçmişi tablosu
-- Ref: arch/docs/qnb-vpos-integration.md

CREATE TABLE IF NOT EXISTS tenant_payments (
    id           BIGSERIAL PRIMARY KEY,
    order_id     VARCHAR(64)    NOT NULL UNIQUE,
    tenant_id    INTEGER        NOT NULL,
    amount       DECIMAL(10,2)  NOT NULL,
    status       VARCHAR(20)    NOT NULL DEFAULT 'pending', -- pending | success | failed
    three_d_status   VARCHAR(10),
    proc_return_code VARCHAR(10),
    auth_code    VARCHAR(50),
    host_ref_num VARCHAR(50),
    trans_id     VARCHAR(50),
    err_msg      TEXT,
    created_at   TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tenant_payments_tenant  ON tenant_payments(tenant_id);
CREATE INDEX IF NOT EXISTS idx_tenant_payments_created ON tenant_payments(created_at DESC);
