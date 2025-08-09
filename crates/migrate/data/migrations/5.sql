-- SCHEMA VERSION 5: 2020-02-14
alter table servers add column log_cleanup_enabled bool not null default false;
update info set schema_version = 5;