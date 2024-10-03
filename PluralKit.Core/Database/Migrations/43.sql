-- database version 43
-- add config setting for padding 5-character IDs in lists

alter table system_config add column hid_list_padding int not null default 0;

update info set schema_version = 43;
