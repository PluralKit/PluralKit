-- database version 38
-- add proxy tag privacy

alter table members add column tts boolean not null default false;

update info set schema_version = 38;