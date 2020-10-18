-- SCHEMA VERSION 11: <insert date> --
-- Add disabling front/latch autoproxy per-member --

alter table members add column disable_autoproxy bool not null default false;