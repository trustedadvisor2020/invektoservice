-- =============================================================
-- Invekto.WebChat Database Schema
-- Service: Invekto.WebChat (port 7113)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- =============================================================

-- =============================================================
-- webchat_visitors: Anonymous website visitors identified by UUID
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_visitors (
    id              TEXT PRIMARY KEY,                    -- UUID stored in browser localStorage
    name            TEXT,                                -- Optional, asked at chat start
    email           TEXT,                                -- Optional, asked at chat start
    first_seen      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    page_url        TEXT,                                -- Last visited page URL
    user_agent      TEXT                                 -- Browser user agent
);

-- =============================================================
-- webchat_conversations: Chat sessions between visitor and operator/AI
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_conversations (
    id              BIGSERIAL PRIMARY KEY,
    visitor_id      TEXT NOT NULL REFERENCES webchat_visitors(id),
    status          VARCHAR(20) NOT NULL DEFAULT 'active',    -- active | ai | closed
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    closed_at       TIMESTAMPTZ,
    last_message_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ai_active       BOOLEAN NOT NULL DEFAULT TRUE             -- AI auto-reply enabled
);

CREATE INDEX ix_webchat_conversations_status ON webchat_conversations(status);
CREATE INDEX ix_webchat_conversations_visitor ON webchat_conversations(visitor_id);

-- =============================================================
-- webchat_messages: Individual chat messages
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_messages (
    id              BIGSERIAL PRIMARY KEY,
    conversation_id BIGINT NOT NULL REFERENCES webchat_conversations(id),
    sender_type     VARCHAR(20) NOT NULL,                     -- visitor | operator | ai
    content         TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_webchat_messages_conv ON webchat_messages(conversation_id, created_at);

-- =============================================================
-- webchat_push_tokens: Expo push notification tokens for operator app
-- =============================================================

CREATE TABLE IF NOT EXISTS webchat_push_tokens (
    id              BIGSERIAL PRIMARY KEY,
    token           TEXT NOT NULL UNIQUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
