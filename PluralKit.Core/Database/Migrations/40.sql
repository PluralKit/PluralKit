-- database version 40
-- add per-server keepproxy toggle

alter table member_guild add column keep_proxy bool default null;

update info set schema_version = 40;