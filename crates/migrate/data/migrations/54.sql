-- database version 54
-- rename blacklist column to proxy blacklist

alter table servers rename column blacklist to proxy_blacklist;

update info set schema_version = 54;