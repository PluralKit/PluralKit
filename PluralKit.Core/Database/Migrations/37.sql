-- database version 37
-- add proxy tag privacy

alter table members add column proxy_privacy integer not null default 1;

update info set schema_version = 37;