-- SCHEMA VERSION 14: 2020-04-01 --
-- added reminders table

CREATE TABLE IF NOT EXISTS reminders
(
    mid bigint NOT NULL PRIMARY KEY,
    channel bigint NOT NULL,
    guild bigint,
    member integer REFERENCES members (id) ON DELETE CASCADE,
    system integer NOT NULL,
    seen boolean NOT NULL,
    timestamp timestamp NOT NULL default (current_timestamp at time zone 'utc'),
);

update info set schema_version = 14; 