-- Add created_at column to matrix_messages for display name dedup ordering
ALTER TABLE matrix_messages ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
CREATE INDEX IF NOT EXISTS idx_matrix_messages_created ON matrix_messages (room_id, created_at DESC);

UPDATE matrix_schema_info SET schema_version = 1;
