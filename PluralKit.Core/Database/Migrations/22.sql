-- schema version 22
-- automatically set members/groups as private when creating

alter table system_config add column member_default_private bool not null default false;
alter table system_config add column group_default_private bool not null default false;

update info set schema_version = 22;