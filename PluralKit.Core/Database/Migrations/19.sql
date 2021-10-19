-- schema version 19: 2021-10-15 --
-- add stats to info table

alter table info add column system_count int;
alter table info add column member_count int;
alter table info add column group_count int;
alter table info add column switch_count int;
alter table info add column message_count int;

update info set schema_version = 19;
