-- database version 48
--
-- add guild settings for disabling "invalid command" responses &
-- enforcing the presence of system tags

alter table servers add column invalid_command_response_enabled bool not null default true;
alter table servers add column require_system_tag bool not null default false;

update info set schema_version = 48;