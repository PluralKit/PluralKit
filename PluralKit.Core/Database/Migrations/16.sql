-- SCHEMA VERSION 16: 2021-08-02 --
-- Add server-specific system tag --

alter table system_guild add column tag text default null;
alter table system_guild add column tag_enabled bool not null default true;

update info set schema_version = 16;