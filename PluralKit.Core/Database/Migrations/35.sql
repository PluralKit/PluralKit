-- database version 35
-- add guild avatar and guild name to system guild settings

alter table system_guild add column avatar_url text;
alter table system_guild add column display_name text;

update info set schema_version = 35;