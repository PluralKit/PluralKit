-- database version 54
-- add short/full option for fronter list

alter table system_config add column fronter_list_format int default 0;

update info set schema_version = 54;