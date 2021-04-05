-- migration 14: add banner (large) images to stuff with "cards"

alter table systems add column banner_image text;
alter table members add column banner_image text;
alter table groups add column banner_image text;

update info set schema_version = 14;
