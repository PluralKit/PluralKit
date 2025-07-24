-- schema version 23
-- show/hide private information when looked up by linked accounts

alter table system_config add column show_private_info bool default true;

update info set schema_version = 23;