-- schema version 31

alter table system_config add column case_sensitive_proxy_tags boolean not null default true;

update info set schema_version = 31;