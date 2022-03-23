-- schema version 28
-- system pronouns

alter table systems add column pronouns text;
alter table systems add column pronoun_privacy integer check (pronoun_privacy in (1, 2)) not null default 1;

update info set schema_version = 28;