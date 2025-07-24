-- database version 32
-- re-add last message timestamp to members

alter table members add column last_message_timestamp timestamp;

update info set schema_version = 32;