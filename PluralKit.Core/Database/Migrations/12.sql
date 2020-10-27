-- SCHEMA VERSION 12: <insert date> --
-- Add disabling front/latch autoproxy per-member --

alter table members add column disable_autoproxy bool not null default false;
alter table systems add column latch_timeout int not null default 6;
update info set schema_version = 12;