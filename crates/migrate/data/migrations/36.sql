-- database version 36
-- add system avatar privacy and system name privacy

alter table systems add column name_privacy integer not null default 1;
alter table systems add column avatar_privacy integer not null default 1;

update info set schema_version = 36;