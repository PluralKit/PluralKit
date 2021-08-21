-- SCHEMA VERSION 17: 2021-08-21 --
-- Add new member welcome message --

alter table systems add column welcome_message text;

-- Same sort of psuedo-enum due to Dapper limitations. See 2.sql.
-- 1 = off
-- 2 = send the message to the current channel
-- 3 = DM the user
-- 4 = send to a specific channel
alter table systems add column welcome_message_mode int check (welcome_message_mode in (1, 2, 3, 4)) not null default 1;

alter table systems add column welcome_message_channel bigint;

update info set schema_version = 17;