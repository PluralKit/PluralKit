-- SCHEMA VERSION 12: <insert date> --
-- Changelog:
-- * Add disabling front/latch autoproxy per-member
-- * Add configurable latch timeout duration
-- * Add disabling autoproxy per-account

alter table members add column disable_autoproxy bool not null default false;
alter table systems add column latch_timeout int not null default 6;
alter table accounts add column disable_autoproxy bool not null default false;
update info set schema_version = 12;