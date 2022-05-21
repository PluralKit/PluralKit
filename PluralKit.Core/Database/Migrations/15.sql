-- SCHEMA VERSION 15: 2021-08-01
-- add banner (large) images to entities with "cards"

alter table systems add column banner_image text;
alter table members add column banner_image text;
alter table groups add column banner_image text;

update info set schema_version = 15;
