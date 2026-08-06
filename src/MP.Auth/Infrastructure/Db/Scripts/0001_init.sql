CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS mp_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    status SMALLINT NOT NULL DEFAULT 0,           -- 0=正常 1=冻结 2=封禁（本版暂不使用）
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS mp_account_bindings (
    id BIGSERIAL PRIMARY KEY,
    account_id UUID NOT NULL REFERENCES mp_accounts(id) ON DELETE CASCADE,
    provider VARCHAR(32) NOT NULL,                -- steam / psn / xbox / nintendo / apple / google / microsoft_store / official / guest
    third_party_id VARCHAR(128) NOT NULL,         -- 各渠道唯一标识；official 存 username，guest 存 device_token
    extra_data JSONB,                             -- official 存 {"password_hash":"...","salt":"..."}；其他渠道预留
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (provider, third_party_id)
);

CREATE INDEX IF NOT EXISTS idx_bindings_account_id ON mp_account_bindings (account_id);
