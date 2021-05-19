-- SCHEMA VERSION 14: <insert date> --
-- Add server-specific system tag --

alter table system_guild add column tag text default null;

update info set schema_version = 14;