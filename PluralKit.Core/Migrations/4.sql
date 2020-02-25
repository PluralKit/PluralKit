-- SCHEMA VERSION 4: 2020-02-12
alter table member_guild add column avatar_url text;
update info set schema_version = 4;