-- database version 33
-- add webhook_avatar_url to system members

alter table members add column webhook_avatar_url text;

update info set schema_version = 33;