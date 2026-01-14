-- database version 55
-- add command blacklist option for servers

alter table servers add column command_blacklist bigint[] not null default array[]::bigint[];

update info set schema_version = 55;