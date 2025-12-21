-- database version 54
-- initial support for premium

alter table system_config add column premium_until timestamp;
alter table system_config add column premium_lifetime bool default false;

update info set schema_version = 54;