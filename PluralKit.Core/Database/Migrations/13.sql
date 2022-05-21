-- SCHEMA VERSION 13: 2021-03-28 --
-- Add system and group colors --

alter table systems add column color char(6);
alter table groups add column color char(6);

update info set schema_version = 13;