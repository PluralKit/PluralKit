-- database version 51
--
-- add guild setting for SUPPRESS_NOTIFICATIONS message flag on proxied messages

alter table servers add column suppress_notifications bool not null default false;

update info set schema_version = 51;