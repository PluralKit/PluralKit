-- database version 49
-- add guild name format

alter table system_guild add column name_format text;

update info set schema_version = 49;