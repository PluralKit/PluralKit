-- schema version 17: 2021-09-26 --
-- add channel_id to command message table

alter table command_messages add column channel_id bigint;
update command_messages set channel_id = 0;
alter table command_messages alter column channel_id set not null;

update info set schema_version = 17;