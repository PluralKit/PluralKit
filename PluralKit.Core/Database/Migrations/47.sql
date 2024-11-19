-- database version 47
-- add config setting for supplying a custom tag format in names

alter table system_config add column name_format text;

update info set schema_version = 47;
