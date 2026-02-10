-- Matrix schema version tracking (independent of the main PK schema)
CREATE TABLE IF NOT EXISTS matrix_schema_info (
    schema_version INT NOT NULL DEFAULT 0
);
INSERT INTO matrix_schema_info (schema_version) VALUES (0);

-- Link Matrix accounts to PK systems (parallel to 'accounts')
CREATE TABLE matrix_accounts (
    mxid            TEXT PRIMARY KEY,
    system          INT REFERENCES systems(id) ON DELETE SET NULL,
    allow_autoproxy BOOL NOT NULL DEFAULT true
);

-- Proxied message tracking (parallel to 'messages')
CREATE TABLE matrix_messages (
    proxied_event_id    TEXT PRIMARY KEY,
    original_event_id   TEXT,
    room_id             TEXT NOT NULL,
    member              INT REFERENCES members(id) ON DELETE SET NULL,
    sender_mxid         TEXT NOT NULL,
    timestamp           TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Virtual user registry
CREATE TABLE matrix_virtual_users (
    member_id       INT PRIMARY KEY REFERENCES members(id) ON DELETE CASCADE,
    mxid            TEXT NOT NULL UNIQUE,
    display_name    TEXT,
    avatar_mxc      TEXT,
    last_synced     TIMESTAMPTZ
);

-- Track which rooms virtual users have joined
CREATE TABLE matrix_virtual_user_rooms (
    mxid        TEXT NOT NULL,
    room_id     TEXT NOT NULL,
    PRIMARY KEY (mxid, room_id)
);

-- Room configuration (parallel to 'servers')
CREATE TABLE matrix_rooms (
    room_id     TEXT PRIMARY KEY,
    log_room    TEXT,
    blacklisted BOOLEAN NOT NULL DEFAULT false
);

-- Autoproxy per room (parallel to 'autoproxy')
CREATE TABLE matrix_autoproxy (
    system               INT NOT NULL REFERENCES systems(id) ON DELETE CASCADE,
    room_id              TEXT NOT NULL DEFAULT '',
    autoproxy_mode       INT NOT NULL DEFAULT 1,
    autoproxy_member     INT REFERENCES members(id) ON DELETE SET NULL,
    last_latch_timestamp TIMESTAMPTZ,
    PRIMARY KEY (system, room_id)
);

-- Transaction idempotency (prevents double-processing on homeserver retries)
CREATE TABLE matrix_transactions (
    txn_id       TEXT PRIMARY KEY,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Indexes
CREATE INDEX idx_matrix_messages_original ON matrix_messages (original_event_id);
CREATE INDEX idx_matrix_messages_room ON matrix_messages (room_id);
CREATE INDEX idx_matrix_messages_sender ON matrix_messages (sender_mxid);
CREATE INDEX idx_matrix_accounts_system ON matrix_accounts (system);
CREATE INDEX idx_matrix_transactions_processed ON matrix_transactions (processed_at);
