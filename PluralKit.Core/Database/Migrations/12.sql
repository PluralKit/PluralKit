-- SCHEMA VERSION 12: 2020-12-08 --
-- Add disabling front/latch autoproxy per-member --
-- Add disabling autoproxy per-account --
-- Add configurable latch timeout --

alter table members add column allow_autoproxy bool not null default true;
alter table accounts add column allow_autoproxy bool not null default true;
alter table systems add column latch_timeout int; -- in seconds

update info set schema_version = 12;