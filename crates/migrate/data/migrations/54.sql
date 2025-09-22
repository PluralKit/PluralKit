-- database version 54
-- add config option for list format

alter table system_config add column fronter_list_format text default 'short';

update info set schema_version = 54;