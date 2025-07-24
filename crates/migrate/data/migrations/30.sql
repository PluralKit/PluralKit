-- schema version 30

alter table system_config add column description_templates text[] not null default array[]::text[];

update info set schema_version = 30;