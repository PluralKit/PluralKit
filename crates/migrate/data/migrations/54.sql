-- database version 54
-- add member aliases

alter table members add column aliases text[] not null default array[]::text[];

update info set schema_version = 54;
