-- Add configurable latch timeout --

alter table systems add column latch_timeout integer not null default 8;
update info set schema_version = 9;