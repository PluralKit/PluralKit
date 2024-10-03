-- database version 41
-- fix statistics counts

alter table info alter column system_count type bigint using system_count::bigint;
alter table info alter column member_count type bigint using member_count::bigint;
alter table info alter column group_count type bigint using group_count::bigint;
alter table info alter column switch_count type bigint using switch_count::bigint;
alter table info alter column message_count type bigint using message_count::bigint;

update info set schema_version = 41;