-- database version 34
-- add proxy_error_message_enabled to system config

alter table system_config add column proxy_error_message_enabled bool default true;

update info set schema_version = 34;