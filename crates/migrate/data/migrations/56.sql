-- database version 55
-- add member alias privacy

alter table members add column alias_privacy int not null default 1 check (alias_privacy = ANY (ARRAY[1,2]));

update info set schema_version = 56;