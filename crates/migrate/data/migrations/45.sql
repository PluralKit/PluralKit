-- database version 45
-- add new config setting "proxy_switch"

alter table system_config add column proxy_switch bool default false;

update info set schema_version = 45;