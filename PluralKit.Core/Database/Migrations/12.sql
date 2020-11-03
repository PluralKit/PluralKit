-- SCHEMA VERSION 12: 2020-11-01 --
-- Add UUIDs for APIs

create extension if not exists pgcrypto;

alter table systems add column uuid uuid default gen_random_uuid();
create index systems_uuid_idx on systems(uuid);

alter table members add column uuid uuid default gen_random_uuid();
create index members_uuid_idx on members(uuid);

alter table switches add column uuid uuid default gen_random_uuid();
create index switches_uuid_idx on switches(uuid);

alter table groups add column uuid uuid default gen_random_uuid();
create index groups_uuid_idx on groups(uuid);

update info set schema_version = 12;