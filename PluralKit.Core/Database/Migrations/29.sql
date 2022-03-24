-- schema version 29

alter table systems add column is_deleting bool default false;

update info set schema_version = 29;